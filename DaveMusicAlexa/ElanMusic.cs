using Alexa.NET;
using Alexa.NET.Request;
using Alexa.NET.Request.Type;
using Alexa.NET.Response;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PrimS.Telnet;
using RestSharp;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace DaveMusicAlexa
{
    public static class ElanMusic
    {
        //The ELAN API
        public static string URL = "https://elan-api-music.azurewebsites.net/playmusic";

        //Get this from SQL Azure
        public static string IPAddress = "";


        [FunctionName("DaveMusicFunction")]

        public static async Task<SkillResponse> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            //log the invocation
            log.LogInformation("DaveMusic HTTP trigger function processed a request.");


            //Create a new HTTP Client
            HttpClient httpclient = new HttpClient();

            //read the http request
            string json = await req.ReadAsStringAsync();

            //deserialize it into skillRequest
            var skillRequest = JsonConvert.DeserializeObject<SkillRequest>(json);

            //check the type of request
            var requestType = skillRequest.GetRequestType();

            //get the user or the person from Alexa
            var systemInfo = skillRequest.Context.System;
            var relevantAlexaId = systemInfo.Person?.PersonId ?? systemInfo.User.UserId;

            //lookup the IPAddress for the user/person
            DatabaseLookup dblookup = new DatabaseLookup();
            IPAddress = dblookup.GetIPAddress(relevantAlexaId);

            //create a skill response to send back to Alexa
            SkillResponse response = null;

            //if it's the launch then do initial processing
            if (requestType == typeof(LaunchRequest))
            {

                //Tell sends a message - use Ask to ask a question
                response = ResponseBuilder.Tell("Welcome to Elan Music. What do you want to play?");


                //by default the conversation ends, use ShouldEndSession = False to keep the dialog open
                response.Response.ShouldEndSession = false;

            }
            //Get the intent request
            else if (requestType == typeof(IntentRequest))
            {
                //initialize artist and room variables
                var artist = "";
                var room = "";

                //check to see which type of intent it is (PlayMusic or SkipMusic)
                var intentRequest = skillRequest.Request as IntentRequest;

                //get the slots (Artist and Room)
                if (intentRequest.Intent.Slots["Artist"].SlotValue != null)
                {
                    //Get the artist name
                    artist = intentRequest.Intent.Slots["Artist"].Value;

                    //convert first character to upper case - Required for Telnet 
                    artist = char.ToUpper(artist[0]) + artist.Substring(1);
                }
                if (intentRequest.Intent.Slots["Room"].SlotValue != null)
                {
                    //get the room name
                    room = intentRequest.Intent.Slots["Room"].Value;

                    //remove the word "the" if it's there. Required for Telnet
                    if (room.Substring(0, 4) == "the ")
                    {
                        room = room.Substring(4);
                    }

                    //convert first character to upper case - Required for Telnet
                    room = char.ToUpper(room[0]) + room.Substring(1);
                }



                //if there's a room then direct the music there.
                if (room.Length > 0)
                {

                    if (intentRequest.Intent.Name == "PlayMusic") {
                        //Have alexa repeat the request using Tell
                        response = ResponseBuilder.Tell($"Playing {artist} in {room}");
                    }
                    else if (intentRequest.Intent.Name == "PlayRadio") {
                        //Have alexa repeat the request using Tell
                        response = ResponseBuilder.Tell($"Playing {artist} radio in {room}");
                }

                    //Leave the dialog open
                    response.Response.ShouldEndSession = true;


                }
                else
                //no room so just change the music
                {
                    //have alexa repeat the command
                    if (response.Response == null)
                    {
                        response = ResponseBuilder.Tell($"Playing {artist}");

                        //leave the dialog open
                        response.Response.ShouldEndSession = true;
                    }
                }

                //Spotify request
                if (intentRequest.Intent.Name == "PlayMusic")
                {

                    var client = new RestClient();
                    client.BaseUrl = new Uri(URL + "?ipaddress=" + IPAddress + "&Service=Spotify&Room=" + room + "&Artist=" + artist);
                    var request = new RestRequest();

                    //Fire and forget the API call
                    functionCallAndForget foo = new functionCallAndForget();
                    Task.Run(() => foo.callAPI(client, request));
                }

                //If it's a radio request ("PlayRadio") then send it to Pandora
                else if (intentRequest.Intent.Name == "PlayRadio") //Pandora
                {
                    try
                    {
                        //Create new Rest Sharp Client
                        var client = new RestClient();

                        //Set the URL and the room
                        client.BaseUrl = new Uri(URL + "?ipaddress=" + IPAddress + "&Service=Pandora&Room=" + room + "&Artist=" + artist);

                        //Create new Rest Sharp Request
                        var request = new RestRequest();

                        //execute the request
                        // IRestResponse myresponse = client.Execute(request);

                        //Fire and forget the API call
                        functionCallAndForget foo = new functionCallAndForget();
                        Task.Run(() => foo.callAPI(client, request));

                    }
                    catch (Exception ex)
                    {
                        throw ex;
                    }

                }
                // if the intent was to skip the song
                else if (intentRequest.Intent.Name == "SkipMusic")
                {
                    //Have alexa repeat the request
                    response = ResponseBuilder.Tell("Skipping Song");

                    //TODO: Replace hardcoded IP address
                    //create telnet client to send request
                    using (Client client = new Client(IPAddress, 5004, new System.Threading.CancellationToken()))
                    {
                        //ping the media server
                        await client.WriteLine("Ping");
                        string s = await client.TerminatedReadAsync("Server=", TimeSpan.FromMilliseconds(1000));
                        Console.WriteLine(s);

                        //Skip the song
                        await client.WriteLine("SKIPNEXT");
                        string u = await client.TerminatedReadAsync(">", TimeSpan.FromMilliseconds(1000));
                        Console.WriteLine(u);
                    }

                }
            }
            //if the session is ending
            else if (requestType == typeof(SessionEndedRequest))
            {
                //have alexa say goodbye
                response = ResponseBuilder.Tell("See you next time!");

                //end the dialog
                response.Response.ShouldEndSession = true;
            }
            return response;
        }



        public class Request
        {
            public string ipaddress { get; set; }
            public string Room { get; set; }
            public string Artist { get; set; }
            public string Service { get; set; }
        }
    }
    public class functionCallAndForget
    {
        public void callAPI(RestClient client, RestRequest restRequest)
        {
            client.Execute(restRequest);

        }
    }
}



