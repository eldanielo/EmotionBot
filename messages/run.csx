#r "Newtonsoft.Json"
#load "EchoDialog.csx"

using System;


using System.Text;
using System.Net;
using System.Threading;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Globalization;

using Microsoft.Bot.Builder.Azure;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;



private const string BaseUrl = "https://westus.api.cognitive.microsoft.com/";

/// <summary>
/// Your account key goes here.
/// </summary>
private const string AccountKey = "beac6283e144462b87062bc44908f2e1";


string[] gifs = new string[] { "https://media.giphy.com/media/l3E6qj3wFoUiXbxuw/giphy.gif", "https://media.giphy.com/media/UO95NWY0PmoWk/giphy.gif", "https://media.giphy.com/media/amUVFzg1wNZKg/giphy.gif" };


public static async Task<object> Run(HttpRequestMessage req, TraceWriter log)
{
    log.Info($"Webhook was triggered!");

    // Initialize the azure bot
    using (BotService.Initialize())
    {
        // Deserialize the incoming activity
        string jsonContent = await req.Content.ReadAsStringAsync();
        var activity = JsonConvert.DeserializeObject<Activity>(jsonContent);

        // authenticate incoming request and add activity.ServiceUrl to MicrosoftAppCredentials.TrustedHostNames
        // if request is authenticated
        if (!await BotService.Authenticator.TryAuthenticateAsync(req, new[] { activity }, CancellationToken.None))
        {
            return BotAuthenticator.GenerateUnauthorizedResponse(req);
        }

        if (activity != null)
        {
            // one of these will have an interface and process it
            switch (activity.GetActivityType())
            {
                case ActivityTypes.Message:
                    // await Conversation.SendAsync(activity, () => new EchoDialog());
                    var c = new ConnectorClient(new Uri(activity.ServiceUrl));
                    using (var client = new HttpClient())
                    {
                        client.BaseAddress = new Uri(BaseUrl);

                        // Request headers.
                        client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", AccountKey);
                        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                        string data = "{\"documents\":[" +
                                    "{\"id\":\"1\",\"text\":\"" + activity.text + "\"},]}";

                        byte[] byteData = Encoding.UTF8.GetBytes(data);
                        var uri = "text/analytics/v2.0/sentiment";
                        var json = await CallEndpoint(client, uri, byteData);
                        dynamic resp = JsonConvert.DeserializeObject(json);
                        var score = resp.documents[0].score;
                        
                        
                        var url = gifs[0];
                        var mood = "happy";
                        if (score < 0.75)
                        {
                            url = gifs[1];
                            mood = "okay";
                            if (score < 0.25)
                            {
                                url = gifs[2];
                                mood = "angry";
                            }
                        }

                        var reactionresponse = await client.GetAsync("http://replygif.net/api/gifs?tag=" + mood + "&api-key=39YAprx5Yi");
                        var reactionjson = await reactionresponse.Content.ReadAsStringAsync();
                        dynamic reactionresp = JsonConvert.DeserializeObject(reactionjson);
                        int random = new Random().Next(0, reactionresp.Count - 2);
                        string reactionurl = reactionresp[random].file;

                        var r = activity.CreateReply();
                        r.Text = "You seem ";
                        r.Attachments.Add(new Attachment()
                        {
                            ContentUrl = reactionurl,
                            ContentType = "image/gif"
                        });

                        await c.Conversations.ReplyToActivityAsync(r);
                    }
                    break;
                case ActivityTypes.ConversationUpdate:

                    break;
                case ActivityTypes.ContactRelationUpdate:
                case ActivityTypes.Typing:
                case ActivityTypes.DeleteUserData:
                case ActivityTypes.Ping:
                default:
                    log.Error($"Unknown activity type ignored: {activity.GetActivityType()}");
                    break;
            }
        }
        return req.CreateResponse(HttpStatusCode.Accepted);
    }



}
static async Task<String> CallEndpoint(HttpClient client, string uri, byte[] byteData)
{
    using (var content = new ByteArrayContent(byteData))
    {
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        var response = await client.PostAsync(uri, content);
        return await response.Content.ReadAsStringAsync();
    }
}
