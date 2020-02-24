using System;

namespace VaultService.S3.Responses.Serializers
{
    public static class DateTimeExtensions
    {
        public static string ToUTC(this DateTime dateTime)
        {
            return dateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        }
    }
}