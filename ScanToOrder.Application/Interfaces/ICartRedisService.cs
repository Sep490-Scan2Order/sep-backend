using System;
using System.Threading.Tasks;

namespace ScanToOrder.Application.Interfaces;

public interface ICartRedisService
{
    Task<string?> GetRawCartAsync(string cartId);
    Task SaveRawCartAsync(string cartId, string json, TimeSpan? expiry = null);
    Task DeleteCartAsync(string cartId);
}

