using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using Microsoft.AspNetCore.Mvc;
using VaultService.S3.Responses.Serializers;

namespace VaultService.S3.Responses
{
    public class S3XmlResponder : IS3Responder
    {
        private readonly IDictionary<Type, IS3Serializer> _serializers;

        public S3XmlResponder(IDictionary<Type, IS3Serializer> serializers)
        {
            this._serializers = serializers;
        }

        public HttpResponseMessage Respond<T>(T t)
        {
            var serializer = GetSerializer(typeof(T));
            var stream = new MemoryStream();
            serializer.Serialize(t, stream);
            var content = new StreamContent(stream);
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK) {Content = content};
            response.Headers.Add("Content-Type", "application/xml");
            return response;
        }

        public ContentResult RespondContent<T>(T t)
        {
            var serializer = GetSerializer(typeof(T));
            var result = serializer.Serialize(t);
            var content = new ContentResult
            {
                ContentType = "application/xml",
                Content = result,
                StatusCode = (int) System.Net.HttpStatusCode.OK
            };
            return content;
        }

        private IS3Serializer GetSerializer(Type type)
        {
            //if (o == null) return new NullSerializer();
            return _serializers.ContainsKey(type) ? _serializers[type] : new NullSerializer();
        }
    }
}