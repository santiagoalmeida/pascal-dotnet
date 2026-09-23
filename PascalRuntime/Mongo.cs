using MongoDB.Bson;
using MongoDB.Driver;

namespace PascalRuntime;

// MongoDB is a document store, not SQL — it doesn't fit DbXxx's cursor-over-SQL-text
// model, so it gets its own module. Documents in/out are plain JSON strings; reading a
// found document's fields reuses the existing JsonGetString/JsonGetInt builtins instead
// of inventing a parallel document-field API.
public static class Mongo
{
    private static IMongoDatabase? _db;
    private static List<BsonDocument>? _results;
    private static int _index;

    public static void Connect(string connectionString, string database)
    {
        var client = new MongoClient(connectionString);
        _db = client.GetDatabase(database);
    }

    public static void Insert(string collection, string jsonDoc)
    {
        var doc = BsonDocument.Parse(jsonDoc);
        _db!.GetCollection<BsonDocument>(collection).InsertOne(doc);
    }

    public static void Find(string collection, string jsonFilter)
    {
        var filter = string.IsNullOrWhiteSpace(jsonFilter) ? new BsonDocument() : BsonDocument.Parse(jsonFilter);
        _results = _db!.GetCollection<BsonDocument>(collection).Find(filter).ToList();
        _index = -1;
    }

    public static bool Next()
    {
        if (_results is null) return false;
        _index++;
        return _index < _results.Count;
    }

    public static string GetDocument() =>
        _results is not null && _index >= 0 && _index < _results.Count ? _results[_index].ToJson() : "";

    public static void Update(string collection, string jsonFilter, string jsonUpdate)
    {
        var filter = BsonDocument.Parse(jsonFilter);
        var update = BsonDocument.Parse(jsonUpdate); // e.g. {"$set": {"campo": "valor"}}
        _db!.GetCollection<BsonDocument>(collection).UpdateMany(filter, update);
    }

    public static void Delete(string collection, string jsonFilter)
    {
        var filter = BsonDocument.Parse(jsonFilter);
        _db!.GetCollection<BsonDocument>(collection).DeleteMany(filter);
    }

    public static int Count(string collection, string jsonFilter)
    {
        var filter = string.IsNullOrWhiteSpace(jsonFilter) ? new BsonDocument() : BsonDocument.Parse(jsonFilter);
        return (int)_db!.GetCollection<BsonDocument>(collection).CountDocuments(filter);
    }
}
