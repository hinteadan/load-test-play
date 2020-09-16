using NBomber.Contracts;
using NBomber.CSharp;
using NBomber.Plugins.Http.CSharp;
using NBomber.Plugins.Network.Ping;
using Newtonsoft.Json;
using System;
using System.Net;
using System.Net.Http;
using System.Text;

namespace LoadTesterPlaygroundConsole
{
    class Program
    {
        const string webDomain = "stage.ebriza.ro";
        const string webProtocol = "https://";
        static readonly RequestInfo[] relativeUrlsToTest = new RequestInfo[] {
            //new RequestInfo{ },
            new RequestInfo{ RelativeUrl = "/ProductStock/Search?id=&types=0&pageItems=1000" },
        };
        static int usersPerSecondToSimulate = 4;
        //static int usersPerSecondToSimulate = 1;
        static readonly TimeSpan defaultSimulationDuration = TimeSpan.FromSeconds(120);
        static readonly TimeSpan defaultWarmupDuration = TimeSpan.FromSeconds(5);

        static readonly HttpClient http;
        static readonly HttpClientHandler httpClientHandler = new HttpClientHandler();

        static Program()
        {
            httpClientHandler.CookieContainer = new CookieContainer();
            httpClientHandler.UseCookies = true;
            http = new HttpClient(httpClientHandler);
        }

        static void Main(string[] args)
        {
            NBomber.Contracts.IStep[] relativeUrlsHttpRequestSteps = new NBomber.Contracts.IStep[relativeUrlsToTest.Length];

            int relativeUrlIndex = -1;
            foreach (RequestInfo requestInfo in relativeUrlsToTest)
            {
                relativeUrlIndex++;
                relativeUrlsHttpRequestSteps[relativeUrlIndex] = HttpStep.Create(name: $"Fetch {requestInfo.RelativeUrl}", context =>
                    {
                        string cookies = httpClientHandler.CookieContainer.GetCookieHeader(new Uri($"{webProtocol}{webDomain}"));
                        NBomber.Http.HttpRequest result 
                            = Http.CreateRequest(requestInfo.Method, $"{webProtocol}{webDomain}{requestInfo.RelativeUrl}")
                            .WithHeader("Cookie", cookies)
                            .WithCheck(async res => res.IsSuccessStatusCode ? Response.Ok() : Response.Fail(res.StatusCode.ToString()))
                            ;
                        if (requestInfo.Body != null)
                        {
                            result = result.WithBody(new StringContent(JsonConvert.SerializeObject(requestInfo.Body), Encoding.UTF8, requestInfo.ContentType));
                        }
                        return result;
                    }
                );
            }

            NBomber.Contracts.Scenario userPerSecondScenario
                = ScenarioBuilder
                .CreateScenario(name: $"Load Testing Scenario for {webDomain} with {usersPerSecondToSimulate} users per second", relativeUrlsHttpRequestSteps)
                .WithWarmUpDuration(defaultWarmupDuration)
                .WithInit(async ctx =>
                {
                    using (HttpResponseMessage response
                        = await http.PostAsync(
                            $"{webProtocol}{webDomain}/Login/Login",
                            new StringContent(
                                JsonConvert.SerializeObject(
                                    new
                                    {
                                        CompanyHandle = "orange",
                                        UserName = "andrew",
                                        Password = "1234",
                                    }
                                ),
                                Encoding.UTF8,
                                "application/json"
                            )
                        )
                    )
                    {
                        response.EnsureSuccessStatusCode();
                    };
                })
                .WithClean(ctx =>
                {
                    http.CancelPendingRequests();
                    httpClientHandler.Dispose();
                    http.Dispose();
                    return System.Threading.Tasks.Task.CompletedTask;
                })
                .WithLoadSimulations(new NBomber.Contracts.LoadSimulation[]{
                    Simulation.InjectPerSec(rate: usersPerSecondToSimulate, during: defaultSimulationDuration),//RATE scenarios per second, for a timespan of DURING seconds)
                });

            PingPluginConfig pingPluginConfig = PingPluginConfig.CreateDefault(new string[] { webDomain });
            PingPlugin pingPlugin = new PingPlugin(pingPluginConfig);

            NBomberRunner
                .RegisterScenarios(userPerSecondScenario)
                .WithTestSuite("Ebriza Load Testing")
                .WithTestName($"Load test {webDomain}")
                .WithWorkerPlugins(pingPlugin)
                .Run();
        }

        class RequestInfo
        {
            public string RelativeUrl { get; set; } = "/";
            public string Method { get; set; } = "GET";
            public object Body { get; set; } = null;
            public string ContentType { get; set; } = "application/json";
        }
    }
}
