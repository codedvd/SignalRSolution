using Elasticsearch.Net;
using Nest;
using SignalRChat.Models;
using SignalRService.Models;

namespace SignalRService.Extensions
{
    public static class ElasticsearchExtensions
    {
        /// <summary>
        /// Adds Elasticsearch services to the service collection.
        /// </summary>
        public static void AddElasticsearchServices(this IServiceCollection services, IConfiguration configuration)
        {
            var url = configuration["ElasticUrl"]!;
            var apiKey = configuration["ElasticAPIKey"]!;
            var defaultIndex = configuration.GetSection("ElasticsearchSettings")["Indexes"]!.Split(",");

            var settings = new ConnectionSettings(new Uri(url))
                .ApiKeyAuthentication(new ApiKeyAuthenticationCredentials(apiKey))
                .EnableDebugMode()
                .PrettyJson();

            var client = new ElasticClient(settings);

            // test the connection
            var pingResponse = client.Ping();
            if (pingResponse.IsValid)
                Console.WriteLine("Connected to Elastic Cloud");
            else
                Console.WriteLine("Connection failed: " + pingResponse.DebugInformation);

            // Register the ElasticClient as a singleton
            services.AddSingleton<IElasticClient>(client);

            // Initialize Elasticsearch indices if they don't exist
            InitializeElasticsearch(client, defaultIndex);
        }

        /// <summary>
        /// Creates the necessary indices and mappings in Elasticsearch.
        /// </summary>
        private static void InitializeElasticsearch(IElasticClient client, string[] indexNames)
        {
            string chatMessagesIndex = indexNames[0];
            string chatRoomsIndex = indexNames[1];
            string userConnectionIndex = indexNames[2];

            // Create ChatMessages index
            if (!client.Indices.Exists(chatMessagesIndex).Exists)
            {
                var response = client.Indices.Create(chatMessagesIndex, c => c
                    .Settings(s => s
                        .NumberOfShards(3)
                        .NumberOfReplicas(1)
                        .Setting("max_result_window", 50000)
                    )
                    .Map<ChatMessage>(m => m
                        .AutoMap()
                        .Properties(p => p
                            .Keyword(k => k.Name(n => n.Id))
                            .Text(t => t
                                .Name(n => n.Username)
                                .Fields(f => f.Keyword(k => k.Name("keyword")))
                            )
                            .Text(t => t
                                .Name(n => n.Room)
                                .Fields(f => f.Keyword(k => k.Name("keyword")))
                            )
                            .Text(t => t.Name(n => n.Content))
                            .Date(d => d
                                .Name(n => n.Timestamp)
                                .Format("strict_date_optional_time||epoch_millis")
                            )
                            .Number(n => n
                                .Name(f => f.MessageType)
                                .Type(NumberType.Integer)
                            )
                        )
                    )
                );

                if (!response.IsValid)
                    throw new Exception($"Failed to create index {chatMessagesIndex}: {response.DebugInformation}");
            }

            // Create ChatRooms index
            if (!client.Indices.Exists(chatRoomsIndex).Exists)
            {
                var createIndexResponse = client.Indices.Create(chatRoomsIndex, c => c
                    .Settings(s => s
                        .NumberOfShards(3)
                        .NumberOfReplicas(1)
                        .Setting("max_result_window", 50000)
                        .Analysis(a => a
                            .Normalizers(n => n
                                .Custom("lowercase_normalizer", cn => cn
                                    .Filters("lowercase")
                                )
                            )
                        )
                    )
                    .Map<ChatRoom>(m => m
                        .AutoMap()
                        .Properties(ps => ps
                            .Keyword(k => k.Name(n => n.Id))
                            .Text(t => t
                                .Name(n => n.RoomName)
                                .Fields(f => f
                                    .Keyword(k => k
                                        .Name("keyword")
                                        .Normalizer("lowercase_normalizer")
                                    )
                                )
                            )
                        )
                    )
                );

                if (!createIndexResponse.IsValid)
                {
                    throw new Exception($"Failed to create index {chatRoomsIndex}: {createIndexResponse.DebugInformation}");
                }
            }


            // Create UserConnection index
            if (!client.Indices.Exists(userConnectionIndex).Exists)
            {
                var response = client.Indices.Create(userConnectionIndex, c => c
                    .Map<UserConnection>(m => m
                        .AutoMap()
                        .Properties(p => p
                            .Text(t => t
                                .Name(n => n.Username)
                                .Fields(f => f.Keyword(k => k.Name("keyword")))
                            )
                            .Text(t => t
                                .Name(n => n.Room)
                                .Fields(f => f.Keyword(k => k.Name("keyword")))
                            )
                        )
                    )
                );

                if (!response.IsValid)
                    throw new Exception($"Failed to create index {userConnectionIndex}: {response.DebugInformation}");
            }
        }
    }
}