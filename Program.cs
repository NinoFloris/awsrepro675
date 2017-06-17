using System;
using System.IO;
using System.Threading.Tasks;
using Amazon;
using Amazon.S3;
using Amazon.S3.Transfer;
using Microsoft.AspNetCore.WebUtilities;

namespace awsrepro
{
    class Program
    {
        private static string S3Key { get; } = "replace";
        private static string S3Secret { get; } = "replace";
        private static string S3Bucket { get; } = "replace";
        private static string S3Region { get; } = "eu-central-1";

        static void Main(string[] args)
        {
            var client = new AmazonS3Client(S3Key, S3Secret, RegionEndpoint.GetBySystemName(S3Region));
            var fileContents = new byte[1024 * 1024]; // 1 MB
            new Random().NextBytes(fileContents);

            try
            {
                UploadLengthNotSupported(fileContents, client).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.Write(ex.GetType().FullName + " ");
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }

            // Just wait for this one, will return at some point with
            // Amazon.S3.AmazonS3Exception: The provided 'x-amz-content-sha256' header does not match what was computed.
            UploadZeroLength(fileContents, client).GetAwaiter().GetResult();
        }

        private static async Task UploadLengthNotSupported(byte[] fileContents, AmazonS3Client client)
        {
            using (var stream = new NotSupportedLengthStream(fileContents))
            using (var transferUtil = new TransferUtility(client))
            {
                await transferUtil.UploadAsync(stream, S3Bucket, "notsupportedexception.bin");
            }
        }

        private static async Task UploadZeroLength(byte[] fileContents, AmazonS3Client client)
        {
            using (var inner = new MemoryStream(fileContents))
            using (var hangingStream = new FileBufferingReadStream(inner, int.MaxValue, default(long?), Path.GetTempPath))
            using (var transferUtil = new TransferUtility(client))
            {
                await transferUtil.UploadAsync(hangingStream, S3Bucket, "hangingupload.bin");
            }
        }

        // Every request stream type out there will not be able to seek or give length, faking one here
        // It is about showing the S3 sdk is not prepared for it and is not giving nice errors in that scenario at all 
        private class NotSupportedLengthStream : MemoryStream
        {
            public NotSupportedLengthStream(byte[] buffer) : base(buffer) { }

            public override bool CanSeek
            {
                get => false;
            }

            public override long Length
            {
                get => throw new NotSupportedException();
            }
        }
    }
}
