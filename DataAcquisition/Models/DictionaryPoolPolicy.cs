using System.Collections.Generic;
using Microsoft.Extensions.ObjectPool;

namespace DataAcquisition.Models;

public class DictionaryPoolPolicy : PooledObjectPolicy<Dictionary<string, object>>
{
    public override Dictionary<string, object> Create()
    {
        return new Dictionary<string, object>();
    }

    public override bool Return(Dictionary<string, object> obj)
    {
        obj.Clear(); // 清空数据，防止复用时污染
        return true;
    }
}