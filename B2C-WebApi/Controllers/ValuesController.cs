using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
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
        private static DateTime BeginningOfTime = new DateTime(2017, 1, 1);
		public static Uri collectionLink;
		public static DocumentClient Client
		{
			get
			{
				if (_client == null)
				{
                    _client = new DocumentClient(new System.Uri(ValuesController.accURL), ValuesController.accKey);
                    collectionLink = UriFactory.CreateDocumentCollectionUri(ValuesController.databaseId, ValuesController.collectionId);
				}
				return _client;
			}
		}
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var scopes = HttpContext.User.FindFirst("http://schemas.microsoft.com/identity/claims/scope")?.Value;
            if (!string.IsNullOrEmpty(Startup.ScopeRead) && scopes != null
                    && scopes.Split(' ').Any(s => s.Equals(Startup.ScopeRead)))
            {
                var result = await GetTodoItemsAsync();
                return Ok(result);
            }
            else
                return Unauthorized();
        }

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
}
