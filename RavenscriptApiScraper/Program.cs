using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Text.Json;
using System.IO;
using System.Text.RegularExpressions;

namespace RavenscriptApiScraper
{
	class Program
	{
		const string DEFAULT_FILE = "ravenscript_api.json";

		static bool AutoQuit = false;
		static string OutputFile = DEFAULT_FILE;

		static void Main(string[] args)
		{
			ParseArguments(args);

			Console.WriteLine("Finding class names");

			string[] classNames = GetClassNames();

			Console.WriteLine($"Found {classNames.Length} classes");

			var classDetails = new List<ClassDetails>();

			foreach (string name in classNames)
			{
				//if (name != "PrimitiveType") continue;

				var details = GetClassDetails(name);

				Console.WriteLine($"{name}");
				Console.WriteLine($"  Fields: {details.Fields.Length}");
				Console.WriteLine($"  Methods: {details.Methods.Length}");

				classDetails.Add(details);
			}

			var output = new Output()
			{
				Classes = classDetails.ToArray(),
			};

			string json = JsonSerializer.Serialize(output);
			File.WriteAllText(OutputFile, json, Encoding.ASCII);

			Console.WriteLine($"Wrote output to: {OutputFile}");

			Quit();
		}

		static void ParseArguments(string[] args)
		{
			if (args.Contains("-h") || args.Contains("--help"))
			{
				PrintHelp();
				Quit();
			}

			AutoQuit = args.Contains("-q");
			OutputFile = ReadArgument(args, "-o", DEFAULT_FILE);
		}

		static string ReadArgument(string[] args, string arg, string defaultValue)
		{
			if (args.Contains(arg))
			{
				string value = args
					.SkipWhile(a => a != arg) // Find arg
					.Last(); // Get value after arg

				if (value == arg)
				{
					Console.WriteLine($"No value was given for: {arg}");
					Console.WriteLine();

					PrintHelp();
					Quit(1);
				}
				else
				{
					return value;
				}
			}

			return defaultValue;
		}

		static void PrintHelp()
		{
			Console.WriteLine($"Usage: RavenscriptApiScraper <arg>");
			Console.WriteLine($"  -o <file> write result to <file> instead of '{DEFAULT_FILE}'");
			Console.WriteLine($"  -q        quit without waiting for keypress");
			Console.WriteLine($"  -h        print this message");
		}

		static void Quit(int exitCode = 0)
		{
			if (!AutoQuit)
			{
				Console.WriteLine();
				Console.WriteLine("Press any key to close ...");
				Console.ReadKey(true);
			}

			Environment.Exit(exitCode);

			throw new Exception($"Exit process with exit code: {exitCode}");
		}

		static string[] GetClassNames()
		{
			var client = new WebClient();

			var classes = new HashSet<string>();

			string html = client.DownloadString("http://ravenfieldgame.com/ravenscript/api.html");
			string[] lines = html.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

			foreach (string line in lines)
			{
				const string NEEDLE = "href=\"api/";

				int start = line.IndexOf(NEEDLE);
				if (start != -1)
				{
					start += NEEDLE.Length; // Skip this part

					int end = line.IndexOf(".html", start);
					string name = line.Substring(start, end - start);

					if (!classes.Contains(name))
					{
						classes.Add(name);
					}
				}
			}

			return classes.ToArray();
		}

		static ClassDetails GetClassDetails(string className)
		{
			var fields = new List<FieldDetails>();
			var methods = new List<MethodDetails>();
			bool isEnum = false;

			var client = new WebClient();

			string rst = client.DownloadString($"http://ravenfieldgame.com/ravenscript/_sources/api/{className}.rst.txt");

			int classStart = rst.IndexOf(".. cpp:class::");
			if (classStart != -1)
			{
				rst = rst.Substring(classStart);
			}
			else
			{
				int enumStart = rst.IndexOf(".. cpp:enum::");
				if (enumStart != -1)
				{
					rst = rst.Substring(enumStart);
					isEnum = true;
				}
				else
				{
					throw new Exception($"Unknown doc type on class: {className}");
				}
			}

			string[] lines = rst.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

			foreach (string line in lines)
			{
				const string C_MEMBER = ".. c:member:: ";
				const string CPP_MEMBER = ".. cpp:member:: ";
				const string FUNCTION = ".. cpp:function:: ";

				int start = line.IndexOf(C_MEMBER);
				if (start != -1)
				{
					start += C_MEMBER.Length; // Skip this
				}
				else
				{
					start = line.IndexOf(CPP_MEMBER);
					if (start != -1)
					{
						start += CPP_MEMBER.Length; // Skip this
					}
				}

				if (start != -1)
				{
					string decl = line.Substring(start);
					string[] parts = decl.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

					bool isStatic = parts.Contains("static") || isEnum;
					bool isConst = parts.Contains("const");

					string name = parts[parts.Length - 1];
					string type = PostProcessType(parts[parts.Length - 2]);

					if (name.Contains("array"))
					{
						throw new Exception("Unexpected array");
					}

					fields.Add(new FieldDetails()
					{
						Name = name,
						Type = type,
						IsStatic = isStatic,
						IsConst = isConst,
					});

					continue;
				}

				start = line.IndexOf(FUNCTION);
				if (start != -1)
				{
					start += FUNCTION.Length; // Skip this

					string decl = line.Substring(start);
					string[] parts = decl.Split(new char[] { ' ', '(', ',', ')' }, StringSplitOptions.RemoveEmptyEntries);

					bool isStatic = parts.Contains("static");
					bool isConst = parts.Contains("const");

					int typeIndex = (isStatic ? 1 : 0) + (isConst ? 1 : 0);

					string type = PostProcessType(parts[typeIndex]);
					string name = parts[typeIndex + 1];
					string[] args = parts
						.Skip(typeIndex + 2) // Skip type and name
						.Batch(2) // Place argument name and type into a bucket
						.Select(s => PostProcessType(s.First()) + " " + s.Last()) // Combine into "type name"
						.ToArray();

					bool isConstructor = name == className;

					if (parts.Contains("operator"))
					{
						continue;
					}

					if (name.Contains("array"))
					{
						throw new Exception("Unexpected array");
					}

					methods.Add(new MethodDetails()
					{
						Name = name,
						ReturnType = type,
						Arguments = args,
						IsStatic = isStatic,
						IsConstructr = isConstructor,
					});
				}
			}

			return new ClassDetails()
			{
				Name = className,
				Fields = fields.ToArray(),
				Methods = methods.ToArray(),
			};
		}

		static string PostProcessType(string type)
		{
			if (!string.IsNullOrWhiteSpace(type))
			{
				const string ARRAY = "array<";
				if (type.StartsWith(ARRAY))
				{
					type = type.Substring(ARRAY.Length, type.Length - ARRAY.Length - 1);
				}
			}

			return type;
		}
	}

	class FieldDetails
	{
		public string Name { get; set; }
		public string Type { get; set; }
		public bool IsStatic { get; set; }
		public bool IsConst { get; set; }
	}

	class MethodDetails
	{
		public string Name { get; set; }
		public string ReturnType { get; set; }
		public string[] Arguments { get; set; }
		public bool IsStatic { get; set; }
		public bool IsConstructr { get; set; }
	}

	class ClassDetails
	{
		public string Name { get; set; }
		public MethodDetails[] Methods { get; set; }
		public FieldDetails[] Fields { get; set; }
	}

	class Output
	{
		public ClassDetails[] Classes { get; set; }
	}

	static class Extensions
	{
		// May produce batches that are smaller than `maxSize`. Not lazy.
		public static IEnumerable<IEnumerable<T>> Batch<T>(this IEnumerable<T> source, int maxSize)
		{
			int n = (int)Math.Ceiling(source.Count() / (double)maxSize);

			for (int i = 0; i < n; i++)
			{
				yield return source.Skip(maxSize * i).Take(maxSize);
			}
		}
	}
}
