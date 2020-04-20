# Ravenscript API Scraper

This projects scrapes the Ravenscrtipt API and stores all members and methods in a JSON file.

## Packages

This project requires [_System.Text.Json_](https://www.nuget.org/packages/System.Text.Json). It should install itself. If not, run:

	Install-Package System.Text.Json -Version 4.7.1

## Usage

	Usage: RavenscriptApiScraper <arg>
	  -o <file> write result to <file> instead of 'ravenscript_api.json'
	  -q        quit without waiting for keypress
	  -h        print this message

## Example output

	{
		Classes: [
			{
				Name: "Vector3",
				Fields: [
					{
						Name: "zero",
						Type: "Vector3",
						IsStatic: true,
						IsConst: true
					}
				],
				Methods: [
					{
						Name: "dot",
						ReturnType: "float",
						Arguments: ["Vector3 a", "Vector3 b"],
						IsStatic: false,
						IsConstructor: false
					}
				]
			}
		]
	}

