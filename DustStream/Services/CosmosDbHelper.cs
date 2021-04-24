using System;
using System.Net;
using System.Collections.Generic;
using System.Threading.Tasks;
using DustStream.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DustStream.Services
{
    public class CosmosDbHelper
    {
        private static readonly string DatabaseId = "duststream";

        private readonly Container CosmosDbContainer;

        public CosmosDbHelper(string connectionString, string containerString)
        {
            CosmosClient cosmosClient = new CosmosClient(connectionString, new CosmosClientOptions() { ApplicationName = "DustStream" });
            Database database = cosmosClient.GetDatabase(DatabaseId);
            CosmosDbContainer = database.GetContainer(containerString);
        }

        public async Task InsertAsync<T>(T item, string partition)
        {
            await this.CosmosDbContainer.CreateItemAsync<T>(item, new PartitionKey(partition));
        }

        public async Task InsertOrReplaceAsync<T>(string partitionString, string keyString, T item)
        {
            try
            {
                // Read the item to see if it exists.  
                ItemResponse<T> existedItem = await this.CosmosDbContainer.ReadItemAsync<T>(keyString, new PartitionKey(partitionString));
                Console.WriteLine("Item in database with id: {0} already exists\n", keyString);

                // Replace the item
                existedItem = await this.CosmosDbContainer.ReplaceItemAsync<T>(item, keyString, new PartitionKey(partitionString));
                Console.WriteLine("Updated item [{0},{1}].\n \tBody is now: {2}\n", partitionString, keyString, existedItem.Resource);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // Create an item in the container
                ItemResponse<T> itemResponse = await this.CosmosDbContainer.CreateItemAsync<T>(item, new PartitionKey(partitionString));
                Console.WriteLine("Created item in database with id: {0} Operation consumed {1} RUs.\n", keyString, itemResponse.RequestCharge);
            }
        }

        public async Task<IEnumerable<T>> QueryItemsAsync<T>(string queryString)
        {
            QueryDefinition queryDefinition = new QueryDefinition(queryString);
            FeedIterator<T> queryResultSetIterator = this.CosmosDbContainer.GetItemQueryIterator<T>(queryDefinition);

            List<T> items = new List<T>();

            while (queryResultSetIterator.HasMoreResults)
            {
                FeedResponse<T> currentResultSet = await queryResultSetIterator.ReadNextAsync();
                foreach (T item in currentResultSet)
                {
                    items.Add(item);
                }
            }

            return items;
        }

        public async Task<T> ReadItemAsync<T>(string partitionString, string keyString)
        {
            try
            {
                ItemResponse<T> itemResponse = await this.CosmosDbContainer.ReadItemAsync<T>(keyString, new PartitionKey(partitionString));
                return itemResponse.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return default(T);
            }
        }
    }
}
