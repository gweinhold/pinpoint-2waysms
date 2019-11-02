using Amazon;
using Amazon.Lambda.Core;
using Amazon.Lambda.SNSEvents;
using Amazon.Pinpoint;
using Amazon.Pinpoint.Model;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

[assembly:LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace AwsDotnetCsharp
{
    public class Handler
    {
        private readonly AmazonPinpointClient client;

        private readonly string region;
        private readonly string originationNumber;
        private readonly string projectId;
        private static readonly string messageType = "TRANSACTIONAL";
        private static readonly string registeredKeyword = "myKeyword";
        private static readonly string senderId = "mySenderId";

        public Handler()
        {
            region = Environment.GetEnvironmentVariable("region");
            originationNumber = Environment.GetEnvironmentVariable("originationNumber");
            projectId = Environment.GetEnvironmentVariable("projectId");
            client = new AmazonPinpointClient(RegionEndpoint.GetBySystemName(region));
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
                        smsResponse.Body = "Here's your report";
                        break;
                    case "ECHO":
                        Console.WriteLine("Echo");
                        smsResponse.Body = incomingBody.Substring(4, incomingBody.Length - 4);
                        break;
                    default:
                        Console.WriteLine("Default");
                        smsResponse.Body = "Thanks for your message";
                        break;
                }

                await SendReplyToSender(smsResponse, destinationNumber);
            }

            return null;
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
