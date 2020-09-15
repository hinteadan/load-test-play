using NBomber.Contracts;
using NBomber.CSharp;
using System;
using System.Threading.Tasks;

namespace LoadTesterPlaygroundConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            // first, you need to create a step
            var step = Step.Create("step", async context =>
            {
                // you can define and execute any logic here,
                // for example: send http request, SQL query etc
                // NBomber will measure how much time it takes to execute your logic

                await Task.Delay(TimeSpan.FromSeconds(1));
                return Response.Ok();
            });

            // second, we add our step to the scenario
            var scenario = ScenarioBuilder.CreateScenario("hello_world", step);

            NBomberRunner
                .RegisterScenarios(scenario)
                .Run();
        }
    }
}
