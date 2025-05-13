using Microsoft.AspNetCore.SignalR;
using Nest;
using SignalRChat.Models;
using SignalRService.Enums;
using SignalRService.Models;

namespace SignalRService.Hubs
{
    public class ChatHub(IElasticClient elasticClient, IConfiguration configuration, ILogger<ChatHub> logger) : Hub
    {
        private readonly ElasticClient _elasticClient = (ElasticClient)elasticClient;
        private readonly ILogger<ChatHub> _logger = logger;
        private readonly int _messageHistoryLimit = configuration.GetValue<int>("ChatSettings:MessageHistoryLimit", 100);

        /// <summary>
        /// Sends a message to all users in the same chat room as the sender and stores it in Elasticsearch.
        /// </summary>
        public async Task SendMessage(string message)
        {
            try
            {
                var connectionResponse = await _elasticClient.GetAsync<UserConnection>(
                Context.ConnectionId,
                g => g.Index("user_connection")
                );

                if (!connectionResponse.Found)
                {
                    _logger.LogWarning("Connection not found for ID {ConnectionId}", Context.ConnectionId);
                    return;
                }

                var userConnection = connectionResponse.Source;

                var chatMessage = new ChatMessage
                {
                    Id = Guid.NewGuid().ToString(),
                    Username = userConnection.Username,
                    Room = userConnection.Room,
                    Content = message,
                    Timestamp = DateTime.UtcNow,
                    MessageType = MessageType.UserMessage
                };

                var indexResponse = await _elasticClient.IndexAsync<ChatMessage>(
                    chatMessage, i => i.Index("chat_messages")
                );

                if (!indexResponse.IsValid)
                {
                    _logger.LogError("Failed to index message: {ErrorReason}", indexResponse.DebugInformation);
                }

                await Clients.Group(userConnection.Room)
                    .SendAsync("ReceiveMessage", userConnection.Username, message);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        /// <summary>
        /// Creates a new chat room and notifies all clients of the updated room list.
        /// </summary>
        public async Task CreateRoom(string roomName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(roomName))
                {
                    await Clients.Caller.SendAsync("RoomCreationFailed", "Room name cannot be empty.");
                    return;
                }

                var lowercasedRoomName = roomName.ToLowerInvariant();
                var response = await _elasticClient.SearchAsync<ChatRoom>(s => s
                    .Index("chat_rooms")
                    .Query(q => q
                        .Term(t => t
                            .Field(f => f.RoomName.Suffix("keyword")) // uses "roomName.keyword"
                            .Value(lowercasedRoomName)
                        )
                    )
                );
                if (response.Documents.Count != 0)
                {
                    await Clients.Caller.SendAsync("RoomCreationFailed", $"Room '{roomName}' already exists.");
                    return;
                }

                var roomCreatedMessage = new ChatRoom
                {
                    Id = Guid.NewGuid().ToString(),
                    RoomName = roomName,
                };
                var indexResponse = await _elasticClient.IndexAsync<ChatRoom>(roomCreatedMessage, i => i.Index("chat_rooms"));

                // Create a system message for the new room
                var systemMessage = new ChatMessage
                {
                    Id = Guid.NewGuid().ToString(),
                    Username = "System",
                    Room = lowercasedRoomName,
                    Content = $"Room '{lowercasedRoomName}' was created",
                    Timestamp = DateTime.UtcNow,
                    MessageType = MessageType.SystemMessage
                };

                // Store the message in Elasticsearch
                await _elasticClient.IndexAsync<ChatMessage>(systemMessage, i => i.Index("chat_messages"));

                var retrieveRooms = await _elasticClient.SearchAsync<ChatRoom>(s => s
                    .Index("chat_rooms")
                    .Query(q => q
                        .MatchAll()
                    )
                    .Sort(so => so
                        .Ascending("roomName.keyword") // Sort alphabetically by room name
                    )
                    .Size(1000) // Adjust if needed
                );

                var chatRooms = retrieveRooms.Documents.Select(rm => rm.RoomName).ToList();
                // Notify all clients of the new room
                await Clients.All.SendAsync("RoomListUpdated", chatRooms);

                // Notify the creator that the room was created successfully
                await Clients.Caller.SendAsync("RoomCreated", roomName);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        /// <summary>
        /// Allows a user to join a specified chat room and loads message history from Elasticsearch.
        /// </summary>
        public async Task JoinRoom(string username, string room)
        {
            // If username is empty, just return available rooms (used when first connecting)
            var retrieveRooms = await _elasticClient.SearchAsync<ChatRoom>(s => s
                .Index("chat_rooms")
                .Query(q => q
                    .MatchAll()
                )
                .Sort(so => so
                    .Ascending("roomName.keyword") // Sort alphabetically by room name
                )
                .Size(1000) // Adjust if needed
            );

            List<string> chatRooms = [.. retrieveRooms.Documents.Select(rm => rm.RoomName)];

            if (string.IsNullOrEmpty(username))
            {
                await Clients.Caller.SendAsync("AvailableRooms", chatRooms);
                return;
            }

            // Check if the specified room exists
            bool roomExists = retrieveRooms.Documents
                 .Any(r => string.Equals(r.RoomName, room, StringComparison.OrdinalIgnoreCase));

            // Default to General room if specified room doesn't exist
            if (!roomExists && !string.IsNullOrEmpty(room))
            {
                _logger.LogInformation($"Room '{room}' not found, defaulting to General");
                room = "General";
            }
            else if (string.IsNullOrEmpty(room))
            {
                room = "General";
            }

            // If already in a room, leave it first
            var connectionResponse = await _elasticClient.GetAsync<UserConnection>(
                Context.ConnectionId,
                g => g.Index("user_connection")
            );
            if (connectionResponse.Found)
            {
                await LeaveCurrentRoom();
            }

            // Join the new room
            var connectionDoc = new UserConnection
            {
                Username = username,
                Room = room
            };
            await _elasticClient.IndexAsync(connectionDoc, i => i
                .Index("user_connection")
                .Id(Context.ConnectionId)
            );

            await Groups.AddToGroupAsync(Context.ConnectionId, room);

            // Create system message for user joining
            var joinMessage = new ChatMessage
            {
                Id = Guid.NewGuid().ToString(),
                Username = "System",
                Room = room,
                Content = $"{username} has joined the room",
                Timestamp = DateTime.UtcNow,
                MessageType = MessageType.SystemMessage
            };

            // Store the join message in Elasticsearch
            await _elasticClient.IndexAsync<ChatMessage>(joinMessage, i => i.Index("chat_messages"));

            // Notify the room that a new user joined
            await Clients.Group(room).SendAsync("UserJoined", username);

            // Load and send message history to the user
            await SendMessageHistoryToUser(room);

            // Send room details to the caller
            await SendRoomDetailsToCaller(room);

            // Log for debugging
            _logger.LogInformation($"User '{username}' joined room '{room}'");
        }

        /// <summary>
        /// Handles user disconnection by removing them from their current room and notifying others in the room.
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var connectionResponse = await _elasticClient.GetAsync<UserConnection>(
                Context.ConnectionId,
                g => g.Index("user_connection")
            );

            if (connectionResponse.Found)
            {
                var userConnection = connectionResponse.Source;

                var leaveMessage = new ChatMessage
                {
                    Id = Guid.NewGuid().ToString(),
                    Username = "System",
                    Room = userConnection.Room,
                    Content = $"{userConnection.Username} has left the room",
                    Timestamp = DateTime.UtcNow,
                    MessageType = MessageType.SystemMessage
                };

                await _elasticClient.IndexAsync<ChatMessage>(
                    leaveMessage,
                    i => i.Index("chat_messages")
                );

                await Clients.Group(userConnection.Room)
                    .SendAsync("UserLeft", userConnection.Username);

                // Delete user connection from Elasticsearch
                await _elasticClient.DeleteAsync<UserConnection>(
                    Context.ConnectionId,
                    d => d.Index("user_connection")
                );
            }

            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Removes the current user from their current chat room and notifies others in the room.
        /// </summary>
        private async Task LeaveCurrentRoom()
        {
            var connectionResponse = await _elasticClient.GetAsync<UserConnection>(
                Context.ConnectionId,
                g => g.Index("user_connection")
            );

            if (!connectionResponse.Found)
                return;

            var userConnection = connectionResponse.Source;

            // Create system message for user leaving
            var leaveMessage = new ChatMessage
            {
                Id = Guid.NewGuid().ToString(),
                Username = "System",
                Room = userConnection.Room,
                Content = $"{userConnection.Username} has left the room",
                Timestamp = DateTime.UtcNow,
                MessageType = MessageType.SystemMessage
            };

            // Store the leave message in Elasticsearch
            await _elasticClient.IndexAsync<ChatMessage>(leaveMessage, i => i.Index("chat_messages"));

            // Remove user from group
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, userConnection.Room);

            // Notify other users
            await Clients.Group(userConnection.Room)
                .SendAsync("UserLeft", userConnection.Username);

            // Optionally: delete the connection from Elasticsearch
            await _elasticClient.DeleteAsync<UserConnection>(
                Context.ConnectionId,
                d => d.Index("user_connection")
            );
        }

        /// <summary>
        /// Sends the list of available rooms and users in the specified room to the caller.
        /// </summary>
        private async Task SendRoomDetailsToCaller(string room)
        {
            var retrieveRooms = await _elasticClient.SearchAsync<ChatRoom>(s => s
               .Index("chat_rooms")
               .Query(q => q
                   .MatchAll()
               ).Sort(so => so
                   .Ascending("roomName.keyword") // Sort alphabetically by room name
               )
               .Size(1000) // Adjust if needed
            );

            var searchResponse = await _elasticClient.SearchAsync<UserConnection>(s => s
                .Index("user_connection")
                .Query(q => q
                    .Term(t => t
                        .Field(f => f.Room.Suffix("keyword"))  // Use keyword for exact match
                        .Value(room)
                    )
                )
                .Size(1000) // adjust if you expect more than 10 users (default size is 10)
            );

            var usersInRoom = searchResponse.Documents
                .Select(u => u.Username)
                .ToList();

            await Clients.Caller.SendAsync("AvailableRooms", retrieveRooms.Documents.Select(r => r.RoomName));
            await Clients.Caller.SendAsync("UsersInRoom", usersInRoom);
            await Clients.Caller.SendAsync("JoinedRoom", room);
        }

        /// <summary>
        /// Retrieves message history from Elasticsearch and sends it to the user who just joined a room.
        /// </summary>
        private async Task SendMessageHistoryToUser(string room)
        {
            try
            {
                var searchResponse = await _elasticClient.SearchAsync<ChatMessage>(s => s
                    .Index("chat_messages")
                    .Query(q => q
                        .Bool(b => b
                            .Must(m => m
                                .Term(t => t
                                    .Field(f => f.Room.Suffix("keyword"))
                                    .Value(room)
                                )
                            )
                        )
                    ).Sort(so => so
                    .Ascending("timestamp")
                    ).Size(_messageHistoryLimit)
                );


                if (!searchResponse.IsValid)
                {
                    _logger.LogError("Failed to retrieve message history: {Error}",
                        searchResponse.DebugInformation);
                    return;
                }

                // Send message history to the user
                if (searchResponse.Documents.Count != 0)
                {
                    await Clients.Caller.SendAsync("MessageHistory",
                        searchResponse.Documents.Select(msg => new
                        {
                            type = msg.MessageType.ToString(),
                            user = msg.Username,
                            text = msg.Content,
                            timestamp = msg.Timestamp
                        }));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving message history for room {Room}", room);
            }
        }

        /// <summary>
        /// Clears the message history for a specified room (admin function).
        /// </summary>
        public async Task ClearRoomHistory(string room, string adminPassword)
        {
            // Simple admin check - in production use proper authentication
            if (adminPassword != "admin123")
            {
                await Clients.Caller.SendAsync("OperationFailed", "Unauthorized access");
                return;
            }

            try
            {
                // Delete messages for the room
                var deleteResponse = await _elasticClient.DeleteByQueryAsync<ChatMessage>(d => d
                    .Index("chat_messages")
                    .Query(q => q
                        .Term(t => t.Room, room)
                    )
                );

                if (deleteResponse.IsValid)
                {
                    // Create system message for history cleared
                    var systemMessage = new ChatMessage
                    {
                        Id = Guid.NewGuid().ToString(),
                        Username = "System",
                        Room = room,
                        Content = "Message history has been cleared",
                        Timestamp = DateTime.UtcNow,
                        MessageType = MessageType.SystemMessage
                    };

                    await _elasticClient.IndexDocumentAsync(systemMessage);
                    await Clients.Group(room).SendAsync("HistoryCleared");
                    await Clients.Group(room).SendAsync("ReceiveMessage", "System", "Message history has been cleared");
                }
                else
                {
                    _logger.LogError("Failed to clear room history: {Error}", deleteResponse.DebugInformation);
                    await Clients.Caller.SendAsync("OperationFailed", "Database operation failed");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing history for room {Room}", room);
                await Clients.Caller.SendAsync("OperationFailed", "An error occurred");
            }
        }
    }
}
