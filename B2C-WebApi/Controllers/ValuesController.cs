using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


namespace B2CWebApi.Controllers
{
    [Microsoft.AspNetCore.Authorization.Authorize]
    [Microsoft.AspNetCore.Mvc.Route("api/[controller]")]
    public class ValuesController : Controller
    {

        public static DocumentClient _client = null;
        public static string databaseId = "ToDoList";
        public static string collectionId = "Items";
        public string permissionId = "Items" + "PK"; // needs to be unique per user
        public string hostURL = "https://b2capidev.azurewebsites.net";
        public static string accURL = "https://b2cdev.documents.azure.com:443/";
        public static string accKey = "4qIWjbeQc3QB8ukxRQXtrYer4VZLVzlwcwXxYVrXt1kqf0s4obpLER49oC3Fx9RTOV2VS9q0vsLCf4S6yAgQTQ==";
        public static Uri collectionLink;
        public static string token;
        private static DateTime BeginningOfTime = new DateTime(2017, 1, 1);

        /// <summary>
        /// Gets the client.
        /// </summary>
        /// <value>The client.</value>
        public static DocumentClient Client
        {
            get
            {
                if (_client == null)
                {
                    _client = new DocumentClient(new System.Uri(ValuesController.accURL),ValuesController.accKey);
                    collectionLink = UriFactory.CreateDocumentCollectionUri(ValuesController.databaseId, ValuesController.collectionId);
                }
                return _client;
            }
        }

        /// <summary>
        /// Get this instance.
        /// <returns>The get.</returns>
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var tokenStr = this.Request.Headers.Single(x => x.Key == "UserID").Value;
            ValuesController.token = tokenStr;
            var scopes = HttpContext.User.FindFirst("http://schemas.microsoft.com/identity/claims/scope")?.Value;
            if (!string.IsNullOrEmpty(Startup.ScopeRead) && scopes != null
                    && scopes.Split(' ').Any(s => s.Equals(Startup.ScopeRead)))
            {
                var res = await GetPermissionByID(tokenStr);
				//FeedResponse<Permission> permFeed = await Client.ReadPermissionFeedAsync(UriFactory.CreateUserUri(ValuesController.databaseId, tokenStr));
                //List<Permission> permList = permFeed.ToList();
                if(res !=null){
                    
                    return Ok(res);
                }

                return Ok(null);
            }
            else
                return Unauthorized();
        }
        /// <summary>
        /// Gets the permission by identifier.
        /// </summary>
        /// <returns>The permission by identifier.</returns>
        /// <param name="userId">User identifier.</param>
        public async Task<PermissionToken> GetPermissionByID(string userId)
        {
            try
            {
                var permissionToken = await GetPermission(userId);
                Console.WriteLine(" returned token " + permissionToken.Token + " with expires " + permissionToken.Expires);
                return permissionToken;
            }
            catch (Exception ex)
            {
                return null;
            }

        }
        /// <summary>
        /// Gets the permission.
        /// </summary>
        /// <returns>The permission.</returns>
        /// <param name="userId">User identifier.</param>
        private async Task<PermissionToken> GetPermission(string userId)
        {
            PermissionToken permissionToken = null;

            try
            {
                permissionToken = await GetCachedUserPermission(userId);
                if (permissionToken != null) return permissionToken;
            }
            catch (DocumentClientException e)
            {
                //if (e.StatusCode != System.Net.HttpStatusCode.NotFound) throw e;
            }

            // Not cached or expired, get a new permission
            permissionToken = await GetNewPermission(userId);

            // Cache it
            await CacheUserPermission(permissionToken);
            return permissionToken;
        }

        /// <summary>
        /// Gets the cached user permission.
        /// </summary>
        /// <returns>The cached user permission.</returns>
        /// <param name="userId">User identifier.</param>
        private async Task<PermissionToken> GetCachedUserPermission(string userId)
        {
            Document permissionDocument = await Client.ReadDocumentAsync(
                    UriFactory.CreateDocumentUri(databaseId, collectionId, userId + "permission"),
                    new RequestOptions
                    {
                        PartitionKey = new PartitionKey(userId)
                    });

            int expires = permissionDocument.GetPropertyValue<int>("expires");
            int fiveMinAgo = Convert.ToInt32(DateTime.UtcNow.AddMinutes(-5).Subtract(BeginningOfTime).TotalSeconds);

            if (expires > fiveMinAgo)
            {
                return new PermissionToken()
                {
                    Token = permissionDocument.GetPropertyValue<string>("token"),
                    Expires = expires,
                    UserId = userId
                };
            }

            return null;
        }
        /// <summary>
        /// Caches the user permission.
        /// </summary>
        /// <returns>The user permission.</returns>
        /// <param name="permissionToken">Permission token.</param>
        private async Task CacheUserPermission(PermissionToken permissionToken)
        {
            permissionToken.Id = permissionToken.UserId + "permission";
            await Client.UpsertDocumentAsync(UriFactory.CreateDocumentCollectionUri(databaseId, collectionId), permissionToken);
        }
        /// <summary>
        /// Gets the new permission.
        /// </summary>
        /// <returns>The new permission.</returns>
        /// <param name="userId">User identifier.</param>
        private async Task<PermissionToken> GetNewPermission(string userId)
        {
            Permission permission = null;
            try
            {
                Uri url = UriFactory.CreatePermissionUri(databaseId, userId, permissionId);
                permission = await Client.ReadPermissionAsync(UriFactory.CreatePermissionUri(databaseId, userId, permissionId));
                
            }
            catch (DocumentClientException e)
            {
                if (e.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    DocumentCollection collection = await Client.ReadDocumentCollectionAsync(UriFactory.CreateDocumentCollectionUri(databaseId, collectionId));
                    Permission p = new Permission
                    {
                        PermissionMode = PermissionMode.All,
                        ResourceLink = collection.SelfLink,
                        ResourcePartitionKey = new PartitionKey(userId),
                        Id = permissionId // Needs to be unique for a given user
                    };
                    await CreateUserIfNotExistsAsync(userId);
                    permission = await Client.CreatePermissionAsync(UriFactory.CreateUserUri(databaseId, userId), p);
                }
                else throw e;
            }
            var expires = Convert.ToInt32(DateTime.UtcNow.Subtract(BeginningOfTime).TotalSeconds) + 3600; // expires in 1h
            return new PermissionToken()
            {
                Token = permission.Token,
                Expires = expires,
                UserId = userId
            };
        }
        /// <summary>
        /// Creates the user if not exists async.
        /// </summary>
        /// <returns>The user if not exists async.</returns>
        /// <param name="userId">User identifier.</param>
        private async Task CreateUserIfNotExistsAsync(string userId)
        {
            try
            {
                await Client.ReadUserAsync(UriFactory.CreateUserUri(databaseId, userId));
            }
            catch (DocumentClientException e)
            {
                if (e.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    await Client.CreateUserAsync(UriFactory.CreateDatabaseUri(databaseId), new User { Id = userId });
                }
            }
        }

        
        /// <summary>
        /// Get the specified id.
        /// </summary>
        /// <returns>The get.</returns>
        /// <param name="id">Identifier.</param>
        // GET api/values/5
        [HttpGet("{id}")]
        public IActionResult Get(int id)
        {
            var scopes = HttpContext.User.FindFirst("http://schemas.microsoft.com/identity/claims/scope")?.Value;
            if (!string.IsNullOrEmpty(Startup.ScopeRead) && scopes != null
                    && scopes.Split(' ').Any(s => s.Equals(Startup.ScopeRead)))
                return Ok("value1");
            else
                return Unauthorized();
        }

        /// <summary>
        /// Post the specified value.
        /// </summary>
        /// <returns>The post.</returns>
        /// <param name="value">Value.</param>
        // POST api/values
        [HttpPost]
        public IActionResult Post([FromBody]string value)
        {
            var scopes = HttpContext.User.FindFirst("http://schemas.microsoft.com/identity/claims/scope")?.Value;
            if (!string.IsNullOrEmpty(Startup.ScopeWrite) && scopes != null
                    && scopes.Split(' ').Any(s => s.Equals(Startup.ScopeWrite)))
                // TODO: Post
                return Ok();
            else
                return Unauthorized();
        }

        /// <summary>
        /// Put the specified id and value.
        /// </summary>
        /// <returns>The put.</returns>
        /// <param name="id">Identifier.</param>
        /// <param name="value">Value.</param>
        // PUT api/values/5
        [HttpPut("{id}")]
        public IActionResult Put(int id, [FromBody]string value)
        {
            var scopes = HttpContext.User.FindFirst("http://schemas.microsoft.com/identity/claims/scope")?.Value;
            if (!string.IsNullOrEmpty(Startup.ScopeWrite) && scopes != null
                    && scopes.Split(' ').Any(s => s.Equals(Startup.ScopeWrite)))
                // TODO: Put
                return Ok();
            else
                return Unauthorized();
        }


        /// <summary>
        /// Delete the specified id.
        /// </summary>
        /// <returns>The delete.</returns>
        /// <param name="id">Identifier.</param>
        // DELETE api/values/5
        [HttpDelete("{id}")]
        public IActionResult Delete(int id)
        {
            var scopes = HttpContext.User.FindFirst("http://schemas.microsoft.com/identity/claims/scope")?.Value;
            if (!string.IsNullOrEmpty(Startup.ScopeWrite) && scopes != null
                    && scopes.Split(' ').Any(s => s.Equals(Startup.ScopeWrite)))
                // TODO: Delete
                return Ok();
            else
                return Unauthorized();
        }

        /// <summary>
        /// Method for getting the list of TotDoItem list
        /// </summary>
        /// <returns>The todo items async.</returns>
        public async Task<List<TodoItem>> GetTodoItemsAsync()
        {
            var Items = new List<TodoItem>();

            try
            {
                var query = Client.CreateDocumentQuery<TodoItem>(collectionLink)
                                  .AsDocumentQuery();
                while (query.HasMoreResults)
                {
                    Items.AddRange(await query.ExecuteNextAsync<TodoItem>());
                }
            }
            catch (DocumentClientException ex)
            {
                Debug.WriteLine("Error: ", ex.Message);
            }

            return Items;
        }

        /// <summary>
        /// Method for saving the totdoitem object
        /// </summary>
        /// <returns>The todo item async.</returns>
        /// <param name="item">Item.</param>
        /// <param name="isNewItem">If set to <c>true</c> is new item.</param>
        public async Task<bool> SaveTodoItemAsync(TodoItem item, bool isNewItem = false)
        {
            try
            {
                if (isNewItem)
                {
                    await Client.CreateDocumentAsync(collectionLink, item);
                    return true;
                }
                else
                {
                    await Client.ReplaceDocumentAsync(UriFactory.CreateDocumentUri(ValuesController.databaseId, ValuesController.collectionId, item.Id), item);
                    return true;
                }
            }
            catch (DocumentClientException ex)
            {
                Debug.WriteLine("Error: ", ex.Message);
                return false;
            }

            return false;
        }
    }

    /// <summary>
    /// Todo item.
    /// </summary>
    public class TodoItem
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }

        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "notes")]
        public string Notes { get; set; }

        [JsonProperty(PropertyName = "done")]
        public bool Done { get; set; }
    }

    public class PermissionToken
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }
        [JsonProperty(PropertyName = "token")]
        public string Token { get; set; }
        [JsonProperty(PropertyName = "expires")]
        public int Expires { get; set; }
        [JsonProperty(PropertyName = "userid")]
        public string UserId { get; set; }
    }
}
