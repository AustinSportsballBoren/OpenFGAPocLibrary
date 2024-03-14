using System.Text.Json;
using Microsoft.VisualBasic;
using OpenFga.Sdk.Client;
using OpenFga.Sdk.Client.Model;
using OpenFga.Sdk.Model;
using Environment = System.Environment;

namespace OpenFGA;

public interface IOpenFGA
{
    public Task AddRelations(List<string> subjects, List<string> relations, List<string> objects, string authModelId = "");
    public Task RemoveRelations(List<string> subjects, List<string> relations, List<string> objects, string authModelId = "");
    public Task<bool?> Check(string sub, string relation, string obj, string authModelId = "");
    public Task<BatchCheckResponse> BatchCheck(List<string> subs, List<string> relations, List<string> objects, string authModelId = "");
    public Task<List<string>> ListObjects(string sub, string relation, string objectType, string authModelId = "");
    public Task<List<string>> ListRelations(string sub, List<string> relations, string obj, string authModelId = "");
    public OpenFGAIds ids { get; set; }
}

public class OpenFGAIds
{
    public string? AuthModelId { get; set; }
    public string? StoreId { get; set; }
}

public class OpenFGA : IOpenFGA
{
    private OpenFgaClient _client;
    public OpenFGAIds ids { get; set; }

    public OpenFGA(string relationshipModel, string? _fgaApiUrl = null, string? _authModelId = null, string? _storeId = null)
    {
        var configuration = new ClientConfiguration()
        {
            ApiUrl = Environment.GetEnvironmentVariable("FGA_API_URL") ?? _fgaApiUrl ?? "http://localhost:8080", // required, e.g. https://api.fga.example
            StoreId = Environment.GetEnvironmentVariable("FGA_STORE_ID") ?? _storeId,// optional, not needed for \`CreateStore\` and \`ListStores\`, required before calling for all other methods
            AuthorizationModelId = Environment.GetEnvironmentVariable("FGA_MODEL_ID") ?? _authModelId, // optional, can be overridden per request
        };

        _client = new OpenFgaClient(configuration);

        if (string.IsNullOrEmpty(_client.StoreId))
        {
            var store = _client.CreateStore(new ClientCreateStoreRequest() { Name = "FGA Demo Store" }).Result;
            _client.StoreId = store.Id;
        }

        if (string.IsNullOrEmpty(_client.AuthorizationModelId))
        {
            string configJson = relationshipModel;
            var body = JsonSerializer.Deserialize<ClientWriteAuthorizationModelRequest>(configJson);
            var response = _client.WriteAuthorizationModel(body!).Result;

            _client.AuthorizationModelId = response.AuthorizationModelId;
        }

        ids = new OpenFGAIds()
        {
            AuthModelId = _client.AuthorizationModelId,
            StoreId = _client.StoreId
        };
    }

    public async Task AddRelations(List<string> subjects, List<string> relations, List<string> objects, string authModelId = "")
    {
        if (string.IsNullOrEmpty(authModelId))
            authModelId = ids.AuthModelId!;

        var options = new ClientWriteOptions() { AuthorizationModelId = authModelId };

        var writes = new List<ClientTupleKey>();

        foreach (var sub in subjects)
        {
            foreach (var relation in relations)
            {
                foreach (var obj in objects)
                {
                    writes.Add(new ClientTupleKey()
                    {
                        User = sub,
                        Relation = relation,
                        Object = obj
                    });
                }
            }
        }

        var body = new ClientWriteRequest()
        {
            Writes = writes
        };

        try
        {
            await _client.Write(body, options);
        }
        catch 
        {
        }
    }

    public async Task RemoveRelations(List<string> subjects, List<string> relations, List<string> objects, string authModelId = "")
    {
        if (string.IsNullOrEmpty(authModelId))
            authModelId = ids.AuthModelId!;

        var options = new ClientWriteOptions() { AuthorizationModelId = authModelId };

        var deletes = new List<ClientTupleKeyWithoutCondition>();

        foreach (var sub in subjects)
        {
            foreach (var relation in relations)
            {
                foreach (var obj in objects)
                {
                    deletes.Add(new ClientTupleKeyWithoutCondition()
                    {
                        User = sub,
                        Relation = relation,
                        Object = obj
                    });
                }
            }
        }

        var body = new ClientWriteRequest()
        {
            Deletes = deletes
        };

        try
        {
            await _client.Write(body, options);
        }
        catch
        {
        }
    }

    public async Task<bool?> Check(string sub, string relation, string obj, string authModelId = "")
    {
        if (string.IsNullOrEmpty(authModelId))
            authModelId = ids.AuthModelId!;

        var options = new ClientWriteOptions() { AuthorizationModelId = authModelId };

        var body = new ClientCheckRequest()
        {
            User = sub,
            Relation = relation,
            Object = obj
        };

        try
        {
            var response = await _client.Check(body, options);
            return response.Allowed;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<string>> ListObjects(string sub, string relation, string objectType, string authModelId = "")
    {
        if (string.IsNullOrEmpty(authModelId))
            authModelId = ids.AuthModelId!;

        var options = new ClientCheckOptions() { AuthorizationModelId = authModelId };

        var body = new ClientListObjectsRequest()
        {
            User = sub,
            Relation = relation,
            Type = objectType
        };

        try
        {
            var response = await _client.ListObjects(body, options);
            return response.Objects;
        }
        catch
        {
            return new List<string>();
        }
    }

    public async Task<List<string>> ListRelations(string sub, List<string> relations, string obj, string authModelId = "")
    {
        if (string.IsNullOrEmpty(authModelId))
            authModelId = ids.AuthModelId!;

        var options = new ClientBatchCheckOptions() { AuthorizationModelId = authModelId };

        var body = new ClientListRelationsRequest()
        {
            User = sub,
            Relations = relations,
            Object = obj
        };

        try
        {
            var response = await _client.ListRelations(body, options);
            return response.Relations;
        }
        catch 
        {
            return new List<string>();
        }
    }

    public async Task<BatchCheckResponse> BatchCheck(List<string> user, List<string> relations, List<string> objects, string authModelId = "")
    {
        if (string.IsNullOrEmpty(authModelId))
            authModelId = ids.AuthModelId!;

        var options = new ClientBatchCheckOptions() { AuthorizationModelId = authModelId };

        List<ClientCheckRequest> checkReqs = new List<ClientCheckRequest>();

        foreach (var relation in relations)
        {
            foreach (var obj in objects)
            {
                foreach (var sub in user)
                {
                    checkReqs.Add(new ClientCheckRequest()
                    {
                        User = sub,
                        Relation = relation,
                        Object = obj
                    });
                }
            }
        }

        return await _client.BatchCheck(checkReqs, options);
    }
}