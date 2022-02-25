using System.Diagnostics;
using System.Xml;
using System.Text.Json;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;
using System.Text;
using static etw_event_dumper.Global;

namespace etw_event_dumper
{   
    internal class EtwDumper
    {      
        private readonly List<string> _providerGuids;
        private readonly List<string> _eventNames;
        private readonly FilterType _filterType;
        private readonly object _filterValue;
        private readonly string _outputFile;
        private readonly object _lock = new();
        private ulong _eventCount = 0;
        private TraceEventSession? tes;
        private ETWTraceEventSource? etes;
        private FileStream? fileStream;
        private StreamWriter? streamWriter;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="providersFile"></param>
        /// <param name="outputFile"></param>
        /// <param name="filterType"></param>
        /// <param name="filterValue"></param>
        /// <param name="eventNamesFile"></param>
        public EtwDumper(
            string providersFile, 
            string outputFile, 
            FilterType filterType, 
            object filterValue, 
            string eventNamesFile)
        {
            _outputFile = outputFile;
            _filterType = filterType;
            _filterValue = filterValue;

            _providerGuids = new List<string>();
            foreach (string line in File.ReadLines(providersFile))
            {
                if (Guid.TryParse(line, out Guid guid))
                {
                    if (!_providerGuids.Contains(guid.ToString()))
                    {
                        _providerGuids.Add(guid.ToString());
                    }
                }
            }

            _eventNames = new List<string>();
            if (eventNamesFile.Length > 0)
            {
                foreach (string line in File.ReadLines(eventNamesFile))
                {
                    if (!_eventNames.Contains(line))
                    {
                        _eventNames.Add(line);
                    }
                }
            }           
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public string Dump()
        {
            if (_providerGuids.Count == 0)
            {
                return "No valid provider guids supplied";
            }

            SetExitHandler();
            Console.WriteLine(String.Empty);

            using (tes = new TraceEventSession("etw-event-dumper", null))
            using (etes = new ETWTraceEventSource("etw-event-dumper", TraceEventSourceType.Session))
            using (fileStream = new FileStream(_outputFile, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite))
            using (streamWriter = new StreamWriter(fileStream, Encoding.UTF8, 65536))
            {
                var dtep = new DynamicTraceEventParser(etes);
                dtep.All += ParseEvents;

                Console.WriteLine("Loading providers");

                foreach (string providerGuid in _providerGuids)
                {
                    tes.EnableProvider(providerGuid);
                }
                Console.WriteLine("Loaded providers, starting ETW dump...");

                etes.Process();
            }

            return "";
        }

        /// <summary>
        /// 
        /// </summary>
        private void SetExitHandler()
        {
            Console.CancelKeyPress += (_, ea) =>
            {
                Cleanup();
                Console.WriteLine("\nReceived SIGINT (Ctrl+C), terminating ETW collection");
                Environment.Exit(0);
            };
        }

        /// <summary>
        /// 
        /// </summary>
        private void Cleanup()
        {
            if (etes != null)
            {
                etes.StopProcessing();
            }

            if (tes != null)
            {
                tes.Stop();
            }    
            
            if (streamWriter != null)
            {
                streamWriter.Flush();
            }

            if (fileStream != null)
            {
                fileStream.Flush();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="data"></param>
        private void ParseEvents(TraceEvent data)
        {
            // Have seen strange behaviour where unspecified ETW providers still send events,
            // through, despite NOT being specified in the config or "enabled" in the app
            if (_providerGuids.Contains(data.ProviderGuid.ToString() ) == false)
            {
                return;
            }

            // Populate Proc name if undefined
            string procName = string.Empty;
            if (string.IsNullOrEmpty(data.ProcessName))
            {
                try
                {
                    procName = Process.GetProcessById(data.ProcessID).ProcessName;
                }
                catch
                {
                    procName = "N/A";
                }
            }

            switch (_filterType)
            {
                case FilterType.Pid:
                    if (data.ProcessID != (int)_filterValue)
                    {
                        return;
                    }
                    break;
                case FilterType.ProcessName:
                    if (data.ProcessName != (string)_filterValue)
                    {
                        return;
                    }
                    break;
            }

            if (_eventNames.Any())
            {
                if (!_eventNames.Contains(data.EventName))
                {
                    return;
                }
            }

            var er = new EventRecord
            {
                ProviderGuid = data.ProviderGuid,
                ProviderName = data.ProviderName,
                EventName = data.EventName,
                Opcode = data.Opcode,
                OpcodeName = data.OpcodeName,
                TimeStamp = data.TimeStamp,
                ThreadID = data.ThreadID,
                ProcessID = data.ProcessID,
                ProcessName = procName
            };

            PrintEventCount();

            var properties = new Dictionary<string, string>();
            try
            {
                StringReader XmlStringContent = new(data.ToString());
                XmlTextReader EventElementReader = new(XmlStringContent);
                string DataValue = string.Empty;
                while (EventElementReader.Read())
                {
                    for (int AttribIndex = 0; AttribIndex < EventElementReader.AttributeCount; AttribIndex++)
                    {
                        EventElementReader.MoveToAttribute(AttribIndex);

                        // Cap maxlen for eventdata elements to 10k
                        if (EventElementReader.Value.Length > 10000)
                        {
                            DataValue = EventElementReader.Value.Substring(0, Math.Min(EventElementReader.Value.Length, 10000));
                            properties.Add(EventElementReader.Name, DataValue);
                        }
                        else
                        {
                            properties.Add(EventElementReader.Name, EventElementReader.Value);
                        }
                    }
                }
            }
            catch
            {
                // For debugging (?), never seen this fail
                properties.Add("XmlEventParsing", "false");
            }
            er.XmlEventData = properties;

            // Serialize to JSON
            string json = JsonSerializer.Serialize<EventRecord>(er);
            if (streamWriter != null)
            {
                streamWriter.WriteLine(json);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="count"></param>
        private void PrintEventCount()
        {
            lock (_lock)
            {
                if (_eventCount == 0)
                {
                    Console.Write($"Events captured: {_eventCount}");
                } 
                else
                {
                    string clear = new('\b', (_eventCount - 1).ToString().Length);

                    Console.Write(clear);
                    Console.Write(_eventCount.ToString());
                }

                _eventCount += 1;
            }
        }
    }
}
