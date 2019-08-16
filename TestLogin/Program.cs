using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eneter.Messaging.DataProcessing.Serializing;
using Eneter.Messaging.EndPoints.StringMessages;
using Eneter.Messaging.MessagingSystems.Composites.AuthenticatedConnection;
using Eneter.Messaging.MessagingSystems.MessagingSystemBase;
using Eneter.Messaging.MessagingSystems.TcpMessagingSystem;

namespace TestLogin
{
    class Program
    {
        private static Dictionary<string, string> myUsers = new Dictionary<string, string>();
        private static IDuplexStringMessageReceiver myReceiver;
        public static MachinesOverview machinesOverview = new MachinesOverview();
        static void Main(string[] args)
        {
            //EneterTrace.TraceLog = new StreamWriter("d:/tracefile.txt");

            // Simulate database with users.
            myUsers["John"] = "password1";
            myUsers["Steve"] = "password2";

            ////machinesOverview = new MachinesOverview();
            //machinesOverview.Availibility = 80;
            //machinesOverview.LineName = "Line 1";
            //machinesOverview.MachineName = "1";
            //machinesOverview.Performance = 70;
            //machinesOverview.Quality = 90;
            //machinesOverview.OEE = 80;
            //ISerializer serializer = new XmlStringSerializer();
            //object mes = serializer.Serialize<MachinesOverview>(machinesOverview);
            //string message = (string)mes;
            //myReceiver.SendResponseMessage(e.ResponseReceiverId, message);

            

            // Create TCP based messaging.
            IMessagingSystemFactory aTcpMessaging = new TcpMessagingSystemFactory();

            // Use authenticated connection.
            IMessagingSystemFactory aMessaging =
                new AuthenticatedMessagingFactory(aTcpMessaging, GetHandshakeMessage, Authenticate);
            IDuplexInputChannel anInputChannel =
                aMessaging.CreateDuplexInputChannel("tcp://192.168.1.5:8060/");

            // Use simple text messages.
            IDuplexStringMessagesFactory aStringMessagesFactory = new DuplexStringMessagesFactory();
            myReceiver = aStringMessagesFactory.CreateDuplexStringMessageReceiver();
            myReceiver.RequestReceived += OnRequestReceived;

            // Attach input channel and start listening.
            // Note: the authentication sequence will be performed when
            //       a client connects the service.
            myReceiver.AttachDuplexInputChannel(anInputChannel);

            Console.WriteLine("Service is running. Press Enter to stop.");
            Console.ReadLine();

            // Detach input channel and stop listening.
            // Note: tis will release the listening thread.
            myReceiver.DetachDuplexInputChannel();
        }

        private static void OnRequestReceived(object sender, StringRequestReceivedEventArgs e)
        {
            // Handle received messages here.

            ISerializer serializer = new XmlStringSerializer();
            TimedRequest timedRequest;
            try
            {
                timedRequest = serializer.Deserialize<TimedRequest>(e.RequestMessage);
                Console.WriteLine("Received message: " + timedRequest.Request);
            }
            catch (Exception ex) {
                Console.WriteLine("Cannot deserialize Request: " + ex.Message);
                return;
            }

            if (timedRequest.Request.Equals("Machines"))
            {

                using (LogstorOEEEntities db = new TestLogin.LogstorOEEEntities())
                {
                    var query = (from KPIPUCS in db.KPI_PU_Current_Summary
                                 join pu in db.Production_Unit on KPIPUCS.PU_Id equals pu.Id
                                 join KPIPUCS1 in db.KPI_PU_Current_Summary on new { KPIPUCS.PU_Id, KPIPUCS.PUKPICT_Id, KPIPUCS.KPICP_Id } equals new { KPIPUCS1.PU_Id, KPIPUCS1.PUKPICT_Id, KPIPUCS1.KPICP_Id }
                                 join KPIPUCS2 in db.KPI_PU_Current_Summary on new { KPIPUCS.PU_Id, KPIPUCS.PUKPICT_Id, KPIPUCS.KPICP_Id } equals new { KPIPUCS2.PU_Id, KPIPUCS2.PUKPICT_Id, KPIPUCS2.KPICP_Id }
                                 join a in db.Areas on pu.A_Id equals a.Id
                                 where KPIPUCS.KPICP_Id == 1 && KPIPUCS.KPICT_Id == 1 && KPIPUCS.KPI_Id == 1 && KPIPUCS.IsDeleted == false && KPIPUCS1.KPI_Id == 2 && KPIPUCS2.KPI_Id == 3
                                 select new { Availibility = KPIPUCS.Value, Performance = KPIPUCS1.Value, Quality = KPIPUCS2.Value, a.NamePL, pu.Name, }).ToList();
                    var machinesList = new List<MachinesOverview>();
                    foreach (var row in query)
                    {
                        MachinesOverview machinesOverview1 = new MachinesOverview();
                        machinesOverview1.Availibility = (row.Availibility == null) ? 0.0f : (float)row.Availibility;
                        machinesOverview1.Performance = (row.Performance == null) ? 0.0f : (float)row.Performance;
                        machinesOverview1.Quality = (row.Quality == null) ? 0.0f : (float)row.Quality;
                        machinesOverview1.MachineName = (string)row.Name;
                        machinesOverview1.LineName = (string)row.NamePL;
                        machinesOverview1.OEE = machinesOverview1.Performance * machinesOverview1.Quality * machinesOverview1.Availibility / 10000;

                        string message = (string)serializer.Serialize<MachinesOverview>(machinesOverview1);
                        myReceiver.SendResponseMessage(e.ResponseReceiverId, message);
                    }
                    myReceiver.SendResponseMessage(e.ResponseReceiverId, "Finished");

                }





            }
            else if (timedRequest.Request.Equals("Areas"))
            {
                using (LogstorOEEEntities db = new TestLogin.LogstorOEEEntities())
                {
                    var query = (from pu in db.Production_Unit
                                 join area in db.Areas on pu.A_Id equals area.Id
                                 where pu.IsDeleted == false
                                 select new { Line = area.NamePL }).Distinct().ToList();
                    string csv = "";
                    for (int i = 0; i < query.Count; i++)
                    {
                        csv = csv + query[i].Line + ",";
                    }
                    csv = csv.Substring(0, csv.Length - 1);
                    //string csv = String.Join(",", query.Select(x => x.Line.ToString().ToArray()));
                    myReceiver.SendResponseMessage(e.ResponseReceiverId, csv);
                }
            }
            else if (timedRequest.Request.Equals("Downtimes"))
            {
                using (LogstorOEEEntities db = new TestLogin.LogstorOEEEntities())
                {
                    DateTime Start = DateTime.ParseExact(timedRequest.StartTime, "yyyy-M-dd H:m", System.Globalization.CultureInfo.InvariantCulture);
                    DateTime End = DateTime.ParseExact(timedRequest.EndTime, "yyyy-M-dd H:m", System.Globalization.CultureInfo.InvariantCulture);
                    var query = (from dh in db.Downtime_History
                                 join pu in db.Production_Unit on dh.PU_Id equals pu.Id
                                 join area in db.Areas on pu.A_Id equals area.Id
                                 join tc in db.Tree_Cause_Config on dh.TCC_Id equals tc.Id
                                 where pu.IsDeleted == false && (dh.StartTime > Start && dh.StartTime < End) || (dh.EndTime == null || (dh.EndTime > Start && dh.EndTime < End))
                                 select new { Line = area.Id, LinePL = area.NamePL, DHStartTime = dh.StartTime, DHEndTime = dh.EndTime, MachineName = pu.Name, Reason = tc.NamePL });


                    if (!timedRequest.LineName.Equals("Wszystkie linie"))
                    {
                        query = query.Where(x => x.LinePL.Equals(timedRequest.LineName));
                        //Where(x => x.Line.Equals(timedRequest.LineName));
                    }
                    var data = query.ToList();

                    foreach (var row in data)
                    {
                        DowntimeEvents downtime = new DowntimeEvents();
                        downtime.lineName = row.Line;
                        downtime.StartDate = row.DHStartTime.ToString("yyyy-M-dd H:m");
                        downtime.EndDate = row.DHEndTime?.ToString("yyyy-M-dd H:m:s");
                        downtime.Reason = row.Reason;
                        downtime.machineName = row.MachineName;

                        string message = (string)serializer.Serialize<DowntimeEvents>(downtime);
                        myReceiver.SendResponseMessage(e.ResponseReceiverId, message);
                    }
                    myReceiver.SendResponseMessage(e.ResponseReceiverId, "Finished");
                }
            }
            else if (timedRequest.Request.Equals("Wastes"))
            {
                using (LogstorOEEEntities db = new TestLogin.LogstorOEEEntities())
                {
                    DateTime Start = DateTime.ParseExact(timedRequest.StartTime, "yyyy-M-dd H:m", System.Globalization.CultureInfo.InvariantCulture);
                    DateTime End = DateTime.ParseExact(timedRequest.EndTime, "yyyy-M-dd H:m", System.Globalization.CultureInfo.InvariantCulture);
                    var query = (from wh in db.Waste_History
                                 join pu in db.Production_Unit on wh.PU_Id equals pu.Id
                                 join area in db.Areas on pu.A_Id equals area.Id
                                 where pu.IsDeleted == false && (wh.TimeStamp > Start && wh.TimeStamp < End)
                                 select new { Line = area.Id, LinePL = area.NamePL, WHTimestamp = wh.TimeStamp, MachineName = pu.Name, Reason = "Strata" });


                    if (!timedRequest.LineName.Equals("Wszystkie linie"))
                    {
                        query = query.Where(x => x.LinePL.Equals(timedRequest.LineName));
                        //Where(x => x.Line.Equals(timedRequest.LineName));
                    }
                    var data = query.ToList();

                    foreach (var row in data)
                    {
                        WasteEvent waste = new WasteEvent();
                        waste.lineNumber = row.Line;
                        waste.date = row.WHTimestamp.ToString("yyyy-M-dd H:m");
                        waste.wasteReason = row.Reason;
                        waste.wasteValue = row.MachineName.ToString();
                        waste.machineNumber = row.MachineName;
                        string message = (string)serializer.Serialize<WasteEvent>(waste);
                        myReceiver.SendResponseMessage(e.ResponseReceiverId, message);
                    }
                    myReceiver.SendResponseMessage(e.ResponseReceiverId, "Finished");
                }
            }
            else if (timedRequest.Request.Equals("ChartMachines"))
            {
                //StateEvent stateEvent = new StateEvent();
                //stateEvent.machines = new List<string>();
                //stateEvent.machines.Add("Maszyna 1");
                //stateEvent.machines.Add("Maszyna 2");
                //stateEvent.lineName = ("Linia 1");
                //string message1 = (string)serializer.Serialize<StateEvent>(stateEvent);
                //myReceiver.SendResponseMessage(e.ResponseReceiverId, message1);


                using (LogstorOEEEntities db = new TestLogin.LogstorOEEEntities())
                {
                    var query = (from pu in db.Production_Unit
                                 join a in db.Areas on pu.A_Id equals a.Id
                                 select new { Line = a.NamePL, LineNumber = a.Id, MachineName = pu.Name, MachineNumber = pu.Id }).ToList();
                    var machinesList = new List<MachinesOverview>();
                    var Lines = query.Select(x => x.Line).Distinct();

                    foreach (var row in Lines)
                    {
                        StateEvent state = new StateEvent();
                        state.lineName = row;

                        var machines = (from q in query
                                        where q.Line.Equals(row)
                                        select new { MachineName = q.MachineName }).ToList();
                        string csv = "";
                        for (int i = 0; i < machines.Count; i++)
                        {
                            csv = csv + machines[i].MachineName + ",";
                        }
                        csv = csv.Substring(0, csv.Length - 1);
                        state.machines = csv;
                        string message = (string)serializer.Serialize<StateEvent>(state);
                        myReceiver.SendResponseMessage(e.ResponseReceiverId, message);
                    }
                    myReceiver.SendResponseMessage(e.ResponseReceiverId, "Finished");

                }
            }
            else if (timedRequest.Request.Equals("ChartData"))
            {
                //StateEvent stateEvent = new StateEvent();
                //stateEvent.machines = new List<string>();
                //stateEvent.machines.Add("Maszyna 1");
                //stateEvent.machines.Add("Maszyna 2");
                //stateEvent.lineName = ("Linia 1");
                //string message1 = (string)serializer.Serialize<StateEvent>(stateEvent);
                //myReceiver.SendResponseMessage(e.ResponseReceiverId, message1);


                using (LogstorOEEEntities db = new TestLogin.LogstorOEEEntities())
                {
                    try
                    {
                        DateTime end = DateTime.ParseExact(timedRequest.EndTime, "yyyy-M-dd H:m", System.Globalization.CultureInfo.InvariantCulture);
                        DateTime start = DateTime.ParseExact(timedRequest.StartTime, "yyyy-M-dd H:m", System.Globalization.CultureInfo.InvariantCulture);
                        var query = (from kh in db.KPI_History
                                     join kh2 in db.KPI_History on new { kh.SH_Id, kh.PU_Id } equals new { kh2.SH_Id, kh2.PU_Id }
                                     join kh3 in db.KPI_History on new { kh.SH_Id, kh.PU_Id } equals new { kh3.SH_Id, kh3.PU_Id }
                                     join pukpi in db.Prod_Unit_KPI on kh.PUKPI_Id equals pukpi.Id
                                     join kpi in db.KPIs on pukpi.KPI_Id equals kpi.Id
                                     join pukpi2 in db.Prod_Unit_KPI on kh2.PUKPI_Id equals pukpi2.Id
                                     join kpi2 in db.KPIs on pukpi2.KPI_Id equals kpi2.Id
                                     join pukpi3 in db.Prod_Unit_KPI on kh3.PUKPI_Id equals pukpi3.Id
                                     join kpi3 in db.KPIs on pukpi3.KPI_Id equals kpi3.Id
                                     join sh in db.Shift_History on kh.SH_Id equals sh.Id
                                     join pu in db.Production_Unit on kh.PU_Id equals pu.Id
                                     join a in db.Areas on pu.A_Id equals a.Id
                                     where pu.Name.Equals(timedRequest.MachineName) && a.NamePL.Equals(timedRequest.LineName) && sh.StartTime >= start
                                        && sh.StartTime < end
                                        && kpi.Name.Equals("Availability") && kpi2.Name.Equals("Performance") && kpi3.Name.Equals("Quality")
                                     select new { LineNumber = a.Id, Timestamp = sh.StartTime, MachineNumber = pu.Id, Availibility = kh.Value, Performance = kh2.Value, Quality = kh3.Value }).ToList();
                        var kPIevent = new KPIevent();
                        kPIevent.timeStamp = "";
                        kPIevent.performanceValue = "";
                        kPIevent.qualityValue = "";
                        kPIevent.oeeValue = "";
                        kPIevent.availabilityValue = "";

                        if (query.Count == 0)
                        {
                            myReceiver.SendResponseMessage(e.ResponseReceiverId, "Finished");
                        }
                        foreach (var row in query)
                        {
                            kPIevent.lineNumber = row.LineNumber;
                            kPIevent.machineNumber = row.MachineNumber;
                            kPIevent.timeStamp += row.Timestamp.ToString("yyyy-M-dd H:m") + ",";
                            kPIevent.performanceValue += row.Performance.ToString() + ",";
                            kPIevent.qualityValue += row.Quality.ToString() + ",";
                            kPIevent.availabilityValue += row.Availibility.ToString() + ",";
                            kPIevent.oeeValue += (row.Performance * row.Availibility * row.Quality / 10000).ToString() + ",";
                        }

                        kPIevent.timeStamp = kPIevent.timeStamp.Substring(0, kPIevent.timeStamp.Length - 1);
                        kPIevent.performanceValue = kPIevent.performanceValue.Substring(0, kPIevent.performanceValue.Length - 1);
                        kPIevent.qualityValue = kPIevent.qualityValue.Substring(0, kPIevent.qualityValue.Length - 1);
                        kPIevent.availabilityValue = kPIevent.availabilityValue.Substring(0, kPIevent.availabilityValue.Length - 1);
                        kPIevent.oeeValue = kPIevent.oeeValue.Substring(0, kPIevent.oeeValue.Length - 1);
                        string message = (string)serializer.Serialize<KPIevent>(kPIevent);
                        myReceiver.SendResponseMessage(e.ResponseReceiverId, message);
                        //myReceiver.SendResponseMessage(e.ResponseReceiverId, null);
                        myReceiver.SendResponseMessage(e.ResponseReceiverId, "Finished");
                    }
                    catch (Exception exc) {
                        myReceiver.SendResponseMessage(e.ResponseReceiverId, "Finished");
                    }

                }
            }

            // Send back the response.
            //myReceiver.SendResponseMessage(e.ResponseReceiverId, "Hello client");
        }

        // Callback which is called when a client sends the login message.
        // It shall verify the login and return the handshake message.
        private static object GetHandshakeMessage(string channelId,
                                                  string responseReceiverId,
                                                  object loginMessage)
        {
            // Find the login name and password in "database"
            // and encrypt the handshake message.
            if (loginMessage is string)
            {
                string aLoginName = (string)loginMessage;

                Console.WriteLine("Received login: " + aLoginName);

                using (LogstorOEEEntities db = new TestLogin.LogstorOEEEntities())
                {
                    Users_Security users_Security = db.Users_Security.Where((x) => x.Login == loginMessage).FirstOrDefault();
                    if (!String.IsNullOrEmpty(users_Security.Password))
                    {
                        string aPassword = users_Security.Password;
                        ISerializer aSerializer = new AesSerializer(aPassword);
                        object aHandshakeMessage = aSerializer.Serialize<string>(Guid.NewGuid().ToString());


                        return aHandshakeMessage;
                    }
                    else
                    {
                        Console.WriteLine("Login was not ok. The connection will be closed.");
                        return null;
                    }
                }
            }
            else {
                return null;
            }

        }

        // Callback which is called when a client sends the handshake response message.
        private static bool Authenticate(string channelId,
                                                string responseReceiverId,
                                                object loginMessage,
                                                object handshakeMessage,
                                                object handshakeResponseMessage)
        {
            string aPassword;
            if (loginMessage is string)
            {

                using (LogstorOEEEntities db = new TestLogin.LogstorOEEEntities())
                {
                    string aLoginName = (string)loginMessage;
                    
                    Users_Security users_Security = db.Users_Security.Where((x) => x.Login == aLoginName).FirstOrDefault();
                    if (!String.IsNullOrEmpty(users_Security.Password))
                    {
                        aPassword = users_Security.Password;
                    }
                    else {
                        return false;
                    }
                }
                        // Get the password associated with the user.
                        
                 

                // Decrypt the handshake response message.
                // Handshake response message is one more time encrypted handshake message.
                // Therefore if the handshake response is decrypted two times it should be
                // the originaly generated GUID.
                try
                {
                    ISerializer aSerializer = new AesSerializer(aPassword);

                    // Decrypt handshake response to get original GUID.
                    string aDecodedHandshakeResponse1 = aSerializer.Deserialize<string>(handshakeResponseMessage);
                    byte[] temp = ConvertHandshakeToBytes(aDecodedHandshakeResponse1);
                    string aDecodedHandshakeResponse2 = aSerializer.Deserialize<string>(temp);

                    // Decrypt original handshake message.
                    string anOriginalGuid = aSerializer.Deserialize<string>(handshakeMessage);

                    // If GUIDs are equal then the identity of the client is verified.
                    if (anOriginalGuid == aDecodedHandshakeResponse2)
                    {
                        Console.WriteLine("Client authenticated.");

                        // The handshake response is correct so the connection can be established.
                        return true;
                    }
                }
                catch (Exception err)
                {
                    // Decoding of the response message failed.
                    // The authentication will not pass.
                    Console.WriteLine("Decoding handshake message failed.", err);
                }
            }

            // Authentication did not pass.
            Console.WriteLine("Authentication did not pass. The connection will be closed.");
            return false;
        }

        private static byte[] ConvertHandshakeToBytes(string handshake) {
            
            string[] lines = handshake.Split(',');
            if (lines.Length == 0)
                return null;
            byte[] result = new byte[lines.Length-1];
            int tempInteger;
            for (int i = 0; i < lines.Length-1; i++) {
                tempInteger = (Int16.Parse(lines[i]) + 256)%256;
                result[i] = (Byte)tempInteger;
            }
            return result;
        }
    }

}
