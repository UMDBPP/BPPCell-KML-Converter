using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
using System.Xml;
using System.IO;
using System.Collections.Generic;

namespace BPPCell_KML_Converter
{
    class Program
    {
        // Indices of various data fields within a given line; indices are 0-based
        // Each line is time, latitude, longitude, altitude, cell signal quality (CSQ)
        private const int INDEX_OF_LAT_IN_LINE = 1; // Latitude
        private const int INDEX_OF_LON_IN_LINE = 2; // Longitude
        private const int INDEX_OF_ALT_IN_LINE = 3; // Altitude
        private const string APRS_STYLE_ID = "aprsformat";

        private const int INDEX_OF_INPUT_FILE = 0; // Index of input filename in args
        private const int INDEX_OF_FLIGHT_NAME = 1; // Index of the flight name in args
        private const int INDEX_OF_OUTPUT_FILE = 2; // Index of (optional) output filename in args
        private const string KML_EXTENSION = ".kml"; // The KML file extension

        private const string HELP_TEXT =
           "\r\nUniversity of Maryland Balloon Payload Program Cell Module KML Converter.\r\nProcesses output from command module cell modem (BPPCELL) GPS logs into KML files for Google Earth.\r\nTakes two  or three arguments.\r\nFirst argument is the input filename. This file must be a valid cell module GPS log file (default name DATALOG.txt).\r\nSecond argument is the flight name/number.\r\nThird (optional) argument is the output filename. This defaults to <inputfilename>.kml";

        static void Main(string[] args)
        {
            // Check if args are valid
            if (args.Length == 0 || args.Length > 3 || CheckArgsForHelp(args))
            {
                Console.WriteLine(HELP_TEXT);
                return;
            }

            // Check input file name
            string inputFileName = args[INDEX_OF_INPUT_FILE];
            if (!File.Exists(inputFileName))
            {
                Console.WriteLine("You appear to have provided an invalid input file name. Please try again.");
                Console.WriteLine(HELP_TEXT);
                return;
            }

            // Create reader for input file
            StreamReader inputFileReader;
            try
            {
                FileStream ins = new FileStream(inputFileName, FileMode.Open);
                inputFileReader = new StreamReader(ins);
            }
            catch (IOException e)
            {
                Console.WriteLine("You appear to have provided an invalid input file name. Please try again.");
                Console.Write("Exception text: {0}", e.Message);
                Console.WriteLine(HELP_TEXT);
                return;
            }

            string flightName = args[INDEX_OF_FLIGHT_NAME];

            // Determine output file name
            string outputFileName = "";
            if (args.Length > INDEX_OF_OUTPUT_FILE)
            {
                outputFileName = args[INDEX_OF_OUTPUT_FILE];
                if (args[INDEX_OF_OUTPUT_FILE].IndexOfAny(Path.GetInvalidPathChars()) >= 0)
                {
                    Console.WriteLine("You appear to have provided an invalid output file name. Please try again.");
                    Console.WriteLine(HELP_TEXT);
                    return;
                }
                
                // Enforce KML file extension
                outputFileName = Path.GetFileNameWithoutExtension(outputFileName);
                outputFileName += KML_EXTENSION;
            }
            else // Behavior if no output file name is provided is to use <inputfilename>.kml
            {
                outputFileName = inputFileName;
                outputFileName = Path.GetFileNameWithoutExtension(outputFileName);
                outputFileName += KML_EXTENSION;
            }

            // Check if output file already exists
            if(File.Exists(outputFileName))
            {
                Console.WriteLine("Output file already exists. Overwrite? y/n");
                ConsoleKeyInfo response = Console.ReadKey();
                if (Char.ToLower(response.KeyChar) != 'y') // End program if user does not wish to overwrite
                    return;
            }
            StreamWriter outputFileWriter;

            try
            {
                FileStream os = new FileStream(outputFileName, FileMode.Create);
                outputFileWriter = new StreamWriter(os);
            }
            catch (IOException e)
            {
                Console.WriteLine("You appear to have provided an invalid output file name. Please try again.");
                Console.Write("Exception text: {0}", e.Message);
                Console.WriteLine(HELP_TEXT);
                return;
            }
            
            CreateKML(inputFileReader, outputFileWriter, 0, flightName); // Begin the conversion process
        }


        /// <summary>
        /// CHecks if the args array contains a help flag
        /// </summary>
        /// <returns>
        /// True if args contains a help flag, false otherwise.
        /// </returns>
        /// A help flag is any one of -?, -h, -H, --help
        /// <param name="args">The args parameter from Main</param>
        private static bool CheckArgsForHelp(string[] args)
        {
            List<String> argsList = new List<String>(args);
            return (argsList.Contains("-?") || argsList.Contains("-h")
                || argsList.Contains("-H") || argsList.Contains("--help"));
        }

        private static void CreateKML(StreamReader input, StreamWriter output, int startingLineNumber, string flightName)
        {
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true;
            settings.IndentChars = "\t";
            using (XmlWriter writer = XmlWriter.Create(output, settings))
            {
                writer.WriteStartElement("kml", "http://www.opengis.net/kml/2.2"); // Required; identifies this as a KML file
                writer.WriteStartElement("Document"); // Required to define shared styles
                writer.WriteAttributeString("id", "aprs"); // Give the document an ID

                WriteStyle(writer);

                WritePlacemark(input, writer, startingLineNumber, flightName);

                writer.WriteEndElement(); // </Document>
                writer.WriteEndElement(); // </kml>

            }
        }

        private static void WritePlacemark(StreamReader input, XmlWriter writer, int startingLineNumber, string flightName)
        {
            writer.WriteStartElement("Placemark");

            writer.WriteStartElement("styleUrl"); // Apply the style
            writer.WriteValue("#" + APRS_STYLE_ID);
            writer.WriteEndElement(); // </styleUrl>

            // Write the name of the placemark
            writer.WriteStartElement("name");
            writer.WriteString(flightName);
            writer.WriteEndElement(); // </name>

            writer.WriteStartElement("MultiGeometry"); // Multiple geometries in this placemark (path and point)

            string[] landingSiteCoords = WriteFlightPath(input, writer, startingLineNumber, extrudePath: true); // Write all the coordinates; get the landing site coordinates

            WriteLandingSiteCoords(writer, landingSiteCoords);

            writer.WriteEndElement(); // </MultiGeometry>

            writer.WriteEndElement(); // </Placemark>
        }

        // Returns the last line as a String array of the elements
        private static string[] WriteFlightPath(StreamReader input, XmlWriter writer, int startingLineNumber, bool extrudePath)
        {

            writer.WriteStartElement("LineString"); // Start the path

            writer.WriteStartElement("tessellate");
            writer.WriteValue(1);
            writer.WriteEndElement(); // </tessellate>

            // Connect path to ground
            writer.WriteStartElement("extrude");
            writer.WriteValue(extrudePath);
            writer.WriteEndElement(); // </extrude>

            // Use MSL altitudes (in m)
            writer.WriteStartElement("altitudeMode");
            writer.WriteValue("absolute");
            writer.WriteEndElement(); // </altitudeMode>

            writer.WriteStartElement("coordinates");

            input.DiscardBufferedData();
            input.BaseStream.Seek(0,SeekOrigin.Begin); // Reset the stream
            for (int i = 0; i < startingLineNumber; i++) // Discard header lines
            {
                input.ReadLine();
            }

            string[] currentLineParts = null;
            while (input.Peek() != -1) // While there are lines to read
            {
                string currentLine = input.ReadLine();
                char[] seperator = { ',' }; // The seperator character for CSV files
                currentLineParts = currentLine.Split(seperator);
                double lat = Double.Parse(currentLineParts[INDEX_OF_LAT_IN_LINE]);
                double lon = Double.Parse(currentLineParts[INDEX_OF_LON_IN_LINE]);
                double alt = Double.Parse(currentLineParts[INDEX_OF_ALT_IN_LINE]);
                if ((lat == 0) && (lon == 0) && (alt == 0)) // Indicates lack of GPS lock; skip this coordinate
                    continue;

                // Write coordinate in KML format
                writer.WriteValue(lon.ToString("00.000000")); // KML is in lon, lat format
                writer.WriteValue(",");
                writer.WriteValue(lat.ToString("00.000000"));
                writer.WriteValue(",");
                writer.WriteValue(alt.ToString("00000.0"));
                writer.WriteValue("\r\n");

            }
            writer.WriteEndElement(); // </coordinates>
            writer.WriteEndElement(); // </LineString>
            return currentLineParts;
        }

        private static void WriteLandingSiteCoords(XmlWriter writer, string[] landingSiteCoords)
        {
            writer.WriteStartElement("Point");

            // Use MSL altitude (in m)
            writer.WriteStartElement("altitudeMode");
            writer.WriteValue("absolute");
            writer.WriteEndElement(); // </altitudeMode>

            writer.WriteStartElement("coordinates");
            string coordToWrite = landingSiteCoords[INDEX_OF_LON_IN_LINE] // The coordinate in KML format; lon before lat
                    + ',' + landingSiteCoords[INDEX_OF_LAT_IN_LINE]
                    + ',' + landingSiteCoords[INDEX_OF_ALT_IN_LINE];
            writer.WriteString(coordToWrite);
            writer.WriteEndElement(); // </Point>

        }

        private static void WriteStyle(XmlWriter writer)
        {
            writer.WriteStartElement("Style");
            writer.WriteAttributeString("id", APRS_STYLE_ID);

            writer.WriteStartElement("LineStyle");
            
            // Set line color
            writer.WriteStartElement("color");
            writer.WriteValue("7fff7200");
            writer.WriteEndElement(); // </color>

            // Set line width
            writer.WriteStartElement("width");
            writer.WriteValue("0.25");
            writer.WriteEndElement(); // </width>

            writer.WriteEndElement(); // </LineStyle>

            writer.WriteStartElement("PolyStyle");

            // Set extrusion color
            writer.WriteStartElement("color");
            writer.WriteValue("40ff7200");
            writer.WriteEndElement(); // </color>

            writer.WriteEndElement(); // </PolyStyle>

            writer.WriteEndElement(); // </Style>
        }
    }
}
