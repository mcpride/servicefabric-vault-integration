using System.Net.Http;
using Microsoft.AspNetCore.Mvc;

namespace VaultService.S3.Responses
{
    public interface IS3Responder
    {
        HttpResponseMessage Respond<TModel>(TModel model);
        ContentResult RespondContent<T>(T t);
    }
}