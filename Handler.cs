using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.Lambda.Core;
using Amazon.Lambda.SNSEvents;
using Amazon.Pinpoint;
using Amazon.Pinpoint.Model;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace AwsDotnetCsharp
{
    public class Handler
    {
        private readonly AmazonPinpointClient client;
        private readonly AmazonDynamoDBClient dBClient;

        private readonly string region;
        private readonly string originationNumber;
        private readonly string projectId;
        private readonly string tableName;
        private static readonly string messageType = "TRANSACTIONAL";
        private static readonly string registeredKeyword = "myKeyword";
        private static readonly string senderId = "mySenderId";

        public Handler()
        {
            region = Environment.GetEnvironmentVariable("region");
            originationNumber = Environment.GetEnvironmentVariable("originationNumber");
            projectId = Environment.GetEnvironmentVariable("projectId");
            client = new AmazonPinpointClient(RegionEndpoint.GetBySystemName(region));
            dBClient = new AmazonDynamoDBClient();
            tableName = Environment.GetEnvironmentVariable("dynamodbTable");
        }

        public async Task<Response> ProcessSms(SNSEvent snsEvent)
        {
            if (snsEvent == null)
            {
                Console.WriteLine("Empty snsEvent object!");
                return null;
            }

            foreach (var record in snsEvent.Records)
            {
                var snsRecord = record.Sns;
                Console.WriteLine($"[{record.EventSource} {snsRecord.Timestamp}] Message = {snsRecord.Message}");

                dynamic reply = JValue.Parse(snsRecord.Message);

                var smsResponse = new SMSMessage
                {
                    MessageType = messageType,
                    OriginationNumber = originationNumber,
                    SenderId = senderId,
                    Keyword = registeredKeyword
                };

                string destinationNumber = reply.originationNumber;
                string incomingBody = reply.messageBody;

                if (incomingBody.IndexOf(' ') <= 0)
                {
                    smsResponse.Body = "Thanks for your message";
                    await SendReplyToSender(smsResponse, destinationNumber);
                    return null;
                }
                
                string prefix = incomingBody.Substring(0, incomingBody.IndexOf(' '));

                switch (prefix.ToUpper())
                {
                    case "REPORT":
                        Console.WriteLine("Getting Report");
                        string reportId = incomingBody.Substring(6, incomingBody.Length - 6);
                        smsResponse.Body = GetReport(reportId).Result;
                        break;
                    case "ECHO":
                        Console.WriteLine("Echo");
                        smsResponse.Body = incomingBody.Substring(4, incomingBody.Length - 4);
                        break;
                    default:
                        Console.WriteLine("Default");
                        smsResponse.Body = "Thanks for your message -1";
                        break;
                }

                await SendReplyToSender(smsResponse, destinationNumber);
            }

            return null;
        }

        private async Task<string> GetReport(string reportId)
        {
            Console.WriteLine($"Getting report for {reportId}.");

            var table = Table.LoadTable(dBClient, tableName);
            var document = await table.GetItemAsync(reportId);
            Console.WriteLine(document["Id"].AsString());

            // var request = new GetItemRequest
            // {
            //     TableName = tableName,
            //     Key = new Dictionary<string, AttributeValue>()
            //     {
            //         { "Id", new AttributeValue { S = reportId } }
            //     }
            // };

            //            var response = await dBClient.GetItemAsync(request);
            var message = $"Report: {document["Id"].AsString()} Created by {document["CreatedBy"].AsString()} on {document["CreateDateTime"].AsString()} Data: {document["Body"].AsString()}";

            Console.WriteLine(message);
            return message;
        }

        private async Task SendReplyToSender(SMSMessage message, string destinationNumber)
        {
            var sendRequest = new SendMessagesRequest
            {
                ApplicationId = projectId,
                MessageRequest = new MessageRequest
                {
                    Addresses = new Dictionary<string, AddressConfiguration>
                        {
                            { destinationNumber, new AddressConfiguration { ChannelType = "SMS" } }
                        },
                    MessageConfiguration = new DirectMessageConfiguration { SMSMessage = message }
                }
            };
            try
            {
                Console.WriteLine($"Sending message: {message.Body} to {destinationNumber}");
                var response = await client.SendMessagesAsync(sendRequest).ConfigureAwait(false);
                Console.WriteLine("Message sent!");
            }
            catch (Exception ex)
            {
                Console.WriteLine("The message wasn't sent. Error message: " + ex.Message);
            }
        }
    }

    public class Response
    {
      public string Message {get; set;}
    }

}
