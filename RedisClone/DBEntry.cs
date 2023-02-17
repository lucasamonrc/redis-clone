namespace RedisClone;

public class DBEntry
{
    public string Key { get; set; }
    public string Value { get; set; }
    public int TTL { get; set; }
    public readonly DateTime CreatedAt = DateTime.Now;
    
    public DBEntry(string key, string value, int ttl = -1)
    {
        Key = key;
        Value = value;
        TTL = ttl;
    }
}
