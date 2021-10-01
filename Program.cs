using BiDataServicesParametersSample.BiDataServices;
using BiDataServicesParametersSample.BiStreamingService;
using System;
using System.IO;
using System.Threading.Tasks;
using CommandLine;

namespace BiDataServicesParametersSample
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            await Parser.Default.ParseArguments<CommandLineOptions>(args)
                .WithParsedAsync(RunReport);
        }
        
        static async Task RunReport(CommandLineOptions options)
        {

            BIDataServiceClient dataClient = new BIDataServiceClient(BIDataServiceClient.EndpointConfiguration.WSHttpBinding_IBIDataService);
            try
            {
                DataContext context = await 

                    dataClient.LogOnAsync(
                        new LogOnRequest
                        {
                            UserName = options.Username,
                            Password = options.Password,
                            ClientAccessKey = options.ClientAccessKey,
                            UserAccessKey = options.UserAccessKey 
                        });

                if (context.Status == ContextStatus.Ok)
                {
                    //note the following four lines set the US-DELIMITER header that causes the report output to be csv using the delimiter you set as the value of that header
                    //valid values are: ",", "SP", "HT", any single character with a decimal ASCII code between 33 and 126
                    //"SP" means space is the delimiter
                    //"HT" means horizontal tab(|) delimited
                    ReportResponse response = null;
                    
                    using (new OperationContextScope(dataClient.InnerChannel))
                    {
                        var httpHeader = new HttpRequestMessageProperty();
                        httpHeader.Headers["US-DELIMITER"] = ",";
                        OperationContext.Current.OutgoingMessageProperties[HttpRequestMessageProperty.Name] = httpHeader;

                        response = await dataClient.ExecuteReportAsync(
                            new ReportRequest
                            {
                                ReportPath = "/content/folder[@name='UltiPro BI Content']/folder[@name='UltiPro BI for Core HR and Payroll']/folder[@name='_UltiPro Delivered Reports']/folder[@name='Human Resources Reports']/report[@name='Employee Birthdays']",
                                ReportParameters = new ReportParameter[]
                                {
                                    new ReportParameter
                                    {
                                        Name = "EmploymentStatus",
                                        Value = "A",
                                        Required = false,
                                        DataType = "xsdString",
                                        MultiValued = true
                                    },

                                    new ReportParameter
                                    {
                                        Name = "Month",
                                        Value = "11",
                                        Required = false,
                                        DataType = "xsdDouble",
                                        MultiValued = true
                                    }
                                }
                            },
                            context);

                        if (response != null && response.Status == ReportRequestStatus.Success)
                        {
                            await GetReportStreamFromResponse(response);
                        }
                        else
                        {
                            Console.WriteLine(response.StatusMessage);
                        }
                    }

                }
                else
                {
                    Console.WriteLine(context.StatusMessage);
                    Console.ReadKey(true);
                }
                await dataClient.LogOffAsync(context);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            finally
            {
                CloseClientProxy(dataClient);
            }
        }

        /// <summary>
        /// Opens a new connection to the streaming service and streams the results
        /// into a file on the consumers file system
        /// </summary>
        /// <param name="response">The response object which contains the ReportRetrievalUri</param>
        private static async Task GetReportStreamFromResponse(ReportResponse response)
        {
            Stream input = null;
            BIStreamServiceClient streamClient = null;
            try
            {
                streamClient = new BIStreamServiceClient(BIStreamServiceClient.EndpointConfiguration.WSHttpBinding_IBIStreamService,
                    new EndpointAddress(response.ReportRetrievalUri));

                StreamReportResponse streamResponse = await streamClient.RetrieveReportAsync(response.ReportKey);
                if (streamResponse.Status == ReportResponseStatus.Failed)
                {
                    Console.WriteLine("Failed to retrieve report due to \"{0}\"", streamResponse.StatusMessage);
                }

                if (streamResponse.Status == ReportResponseStatus.Working)
                {
                    Console.WriteLine("Working to retrieve report due to \"{0}\"", streamResponse.StatusMessage);
                }

                char[] separators = {','};
                using (StreamReader reader = new StreamReader(streamResponse.ReportStream))
                {
                    using (Stream output = new FileStream("Birthdays.csv", FileMode.Create, FileAccess.Write))
                    {
                        using (StreamWriter writer = new StreamWriter(output))
                        {
                            int bytesRead;
                            char[] buffer = new char[1024];
                            while ((bytesRead = reader.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                writer.Write(buffer, 0, bytesRead);
                            }
                        }
                    }
                }
            }
            finally
            {
                input?.Close();
                CloseClientProxy(streamClient);
            }
        }

        private static void CloseClientProxy(ICommunicationObject client)
        {
            if (client == null) return;

            if (client.State != CommunicationState.Faulted)
            {
                try
                {
                    client.Close();
                }
                catch
                {
                    client.Abort();
                }
            }
            else
            {
                client.Abort();
            }
        }
    }
}