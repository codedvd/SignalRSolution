using MongoDB.Driver;
using SignalRService.Models;

namespace SignalRService.Context
{
    public class MongoDbContext
    {
        private readonly IMongoDatabase _database;
        private readonly IConfiguration _config;
        public MongoDbContext(IConfiguration config)
        {
            _config = config;
            var client = new MongoClient(_config!["MongoConnection"]);
            _database = client.GetDatabase(_config["MongoDbName"]);
        }

        public IMongoCollection<WhitelistedIP> WhitelistedIPs => _database.GetCollection<WhitelistedIP>("WhitelistedIPs");
    }
}
