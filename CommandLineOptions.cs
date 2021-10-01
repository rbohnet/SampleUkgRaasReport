using CommandLine;

namespace BiDataServicesParametersSample
{
    public class CommandLineOptions 
    {
        [Value(index: 0, Required = true, HelpText = "Username")]
        public string Username{ get; set; }

        [Value(index: 1, Required = true, HelpText = "Password")]
        public string Password{ get; set; }

        [Value(index: 2, Required = true, HelpText = "Client Access Key")]
        public string ClientAccessKey { get; set; }

        [Value(index: 3, Required = true, HelpText = "User Access Key")]
        public string UserAccessKey { get; set; }
    }
}
