using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Drawing;
using System.Threading;
using WinForms = System.Windows.Forms;

using Ranorex;
using Ranorex.Core;
using Ranorex.Core.Testing;

using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace XmlFileValidation
{
	/// <summary>
	/// Xml file validation for Ranorex. Developed by Dominik Zerbe.
	/// For more information: https://github.com/DominikZerbe/MyRanorexToolbox/
	/// </summary>
	[UserCodeCollection]
	public class XmlValidation
	{

		#region CoreProperties

		/// <summary>
		/// The file that is used as an error-free confirmed reference file for testing the test file.
		/// Please use the load method to fill these values.
		/// </summary>
		public static XDocument ReferenceFile;

		/// <summary>
		/// The file to be checked
		/// Please use the load method to fill these values
		/// </summary>  
		public static XDocument TestFile;

		/// <summary>
		/// Used by some methods to distinguish between reference or test file.
		/// </summary>
		public enum XFileType
		{
			Reference = 0,
			Test = 1
		}

		#endregion

		/// <summary>
		/// Every file with this extension will be regognized as supported.
		/// </summary>
		private static List<string> SupportedFileTypes = new List<string>()
		{
			".xml"
		};

		/// <summary>
		/// Every log entry uses this value as category.
		/// </summary>
		private const string ReportCategory = "Xml Validation";

		/// <summary>
		/// XElement with dynamic values like uuids or timestamps etc.
		/// These elements will be checked without value validation.
		/// </summary>
		private static List<string> DynamicXmlElements = new List<string>();

		/// <summary>
		/// These XElement will be ignored in the test.
		/// </summary>
		private static List<string> IgnoredXmlElements = new List<string>();

		#region ConfigProperties
		/// <summary>
		/// To get more informations by the ranorex report.
		/// </summary>
		public static bool IsDebug = false;

		/// <summary>
		/// Controls whether the entire XML path is used for the 
		/// validation of individual xml elements
		/// </summary>
		public static bool StrictPathMatching = true;

		/// <summary>
		/// Information that is output in the report can also 
		/// be attached to the test file as a comment
		/// </summary>
		public static bool CommentTestFileOnError = false;

		/// <summary>
		/// Controls whether the test file is output in the report
		/// in the event of a failure
		/// </summary>
		public static bool ReportTestfileOnError = false;

		/// <summary>
		/// Should the testfile be exported on failure
		/// </summary>
		public static bool SaveTestFileOnError = false;

		/// <summary>
		/// The export path for the testfile on failure.
		/// </summary>
		public static string SaveTestFilePath;

		/// <summary>
		/// This value is filled when the test file is loaded with the Load method.
		/// If the test file is exported in the event of a failure, this file name is used.
		/// </summary>
		public static string SaveTestFileName;

		public static bool IsTestFailed {get; private set;}

		#endregion


		#region SearchXmlFile
		/// <summary>
		/// Searches for the first matching file from the file system that contains 
		/// all passed xml elements or returns null if no file matches
		/// </summary>
		/// <param name="path">The path to the dictionary to search for</param>
		/// <param name="matchingPatterns">A list of xml elements that must be included in the file</param>
		/// <returns>The path to the file containing all elements or null</returns>
		[UserCodeMethod]
		public static string GetFirstMatchOrNull(string path, List<XElement> matchingPatterns)
		{

			if (path == null)
			{
				throw new ArgumentNullException("path is null");
			}

			if (matchingPatterns == null || matchingPatterns.Count == 0)
			{
				throw new ArgumentNullException("matching patterns is null or empty");
			}

			if(!System.IO.Directory.Exists(path))
			{
				Report.Failure(ReportCategory,string.Format("The dictionary does not exists: {0}",path));
				return null;
			}

			var supportedFiles = GetSupportedFilesFromPath(path);

			// We take the first file, which contains all the 
			// corresponding elements, with the corresponding values
			foreach (string file in supportedFiles)
			{

				XDocument xmlFile = XDocument.Load(file);

				bool keyFound = true;
				foreach (XElement searchedElement in matchingPatterns)
				{

					// If no element is found, we do not need to continue working 
					// with the element, as all elements must match
					if (FindXmlInfoObject(xmlFile, searchedElement, StrictPathMatching, true).Count == 0)
					{
						LogDebug("GetFirstMatchOrNUll", string.Format("'{0}' does not match with '{1}'", file, searchedElement.Name.LocalName));
						keyFound = false;
						break;
					}

				}

				if (keyFound)
				{
					LogDebug("GetFirstMatchOrNUll", string.Format("Matching file was found: {0}", file));
					return file;
				}

			}

			// If no file was found
			return null;

		}

		/// <summary>
		/// Returns the only supported file from the specified directory. 
		/// This method should only be used if it can be guaranteed that there is only one supported file.
		/// Unsupported files are ignored.
		/// </summary>
		/// <param name="path">The path to the dictionary</param>
		/// <param name="failOnMultipleFiles">If several files are found, there is a Failure in the log and the return value zero. Otherwise there is a warning and the first possible return value (bad practice).</param>
		/// <returns></returns>
		[UserCodeMethod]
		public static string GetSingleFileFromPath(string path, bool failOnMultipleFiles = true)
		{

			if (path == null)
			{
				throw new ArgumentNullException("path is null");
			}

			if(!System.IO.Directory.Exists(path))
			{
				Report.Failure(ReportCategory,string.Format("The dictionary does not exists: {0}",path));
				return null;
			}

			var supportedFiles = GetSupportedFilesFromPath(path);

			if (supportedFiles.Count == 0)
			{

				Report.Failure(ReportCategory, "No supported file could be found");
				return null;

			}
			else if (supportedFiles.Count == 1)
			{

				LogDebug("GetSingleFileFromPath", string.Format("Found one supported file: {0}", supportedFiles[0]));
				return supportedFiles[0];

			}

			// At this point, there are more files than one in the dictionary!

			Report.Log(level: failOnMultipleFiles ? ReportLevel.Failure : ReportLevel.Warn,
					category: ReportCategory,
					message: string.Format("Several files are found in the directory that are supported. There should only be one. Path: {0}", path));

			if (failOnMultipleFiles)
			{

				return null;

			}
			else
			{
					
				// Ideally, this should not be used, but I leave it in anyway. 
				Report.Warn(ReportCategory, string.Format("The first matching file is returned by chance: {0}", supportedFiles[0]));
				return supportedFiles[0];

			}		

		}



		#endregion

		#region ConfigFunctions
		/// <summary>
		/// Adds a file extension to the list of supported file extensions.
		/// (Experimental: Never tested!)
		/// </summary>
		/// <param name="fileExtension">The file extension (.xml)</param>
		[UserCodeMethod]
		public static void AddSupportedFileExtension(string fileExtension)
		{

			if (string.IsNullOrEmpty(fileExtension))
			{
				throw new ArgumentNullException("fileExtension is null");
			}

			if (!SupportedFileTypes.Any(f =>
				f.Replace(".", "").ToLower() == fileExtension.Replace(".", "").ToLower()))
			{
				LogDebug("AddSupportedFileExtension", string.Format("{0} was added to the list of supported file extensions.", fileExtension));
				SupportedFileTypes.Add(fileExtension);
			}
			else
			{
				LogDebug("AddSupportedFileExtension", string.Format("This file extension exists already in the list of supported file extensions: {0}", fileExtension));
			}

		}

		/// <summary>
		/// Adds an element to the list of dynamic elements
		/// </summary>
		/// <param name="xmlElement">The xml element to be added</param>
		[UserCodeMethod]
		public static void AddDynamicXmlElement(XElement xmlElement)
		{

			if (xmlElement == null)
			{
				throw new ArgumentNullException("the xml Element is null");
			}

			if (!DynamicXmlElements.Any(x => x.ToLower() == xmlElement.Name.LocalName.ToLower()))
			{
				Report.Info(ReportCategory, string.Format("The element '{0}' was added to the list of dynamic elements", xmlElement.Name.LocalName));
				DynamicXmlElements.Add(xmlElement.Name.LocalName);
			}
		}


		/// <summary>
		/// Adds an element to the list of ignored elements
		/// </summary>
		public static void AddIgnoredXmlElement(XElement xmlElement)
		{

			if (xmlElement == null)
			{
				throw new ArgumentNullException("the xml Element is null");
			}

			if (!IgnoredXmlElements.Any(x => x.ToLower() == xmlElement.Name.LocalName.ToLower()))
			{
				Report.Info(ReportCategory, string.Format("The element '{0}' was added to the list of ignored elements", xmlElement.Name.LocalName));
				IgnoredXmlElements.Add(xmlElement.Name.LocalName);
			}

		}

		#endregion

		#region XmlFunctions

		/// <summary>
		/// Returns all parent xml elements of an XElement in a list.
		/// </summary>
		/// <param name="childElement">The child xml element to start with.</param>
		/// <returns>A list of xml info objects. The first element is the element in the highest level. The last element is the start element.</returns>
		private static List<XmlInfoObject> GetAllParents(XElement childElement)
		{

			if (childElement == null)
			{
				throw new ArgumentNullException("the child element was null.");
			}

			var allParents = new List<XmlInfoObject>();

			// Traverse up the hierarchy to collect all parent elements
			while (childElement.Parent != null)
			{

				// This object is designed for internal processing
				XmlInfoObject xmlInfoObject = new XmlInfoObject
				(
					xmlElement: childElement,
					isDynamic: DynamicXmlElements.Contains(childElement.Name.LocalName)
				);

				allParents.Add(xmlInfoObject);

				// So that the loop can continue to run with the current object
				childElement = childElement.Parent;
			}

			// Reverse the list to have the highest-level element first
			allParents.Reverse();

			return allParents;

		}

		/// <summary>
		/// Returns a list of all xml info objects found. 
		/// </summary>
		/// <param name="xmlDocument">The document to be searched</param>
		/// <param name="xmlElement">The element to be searched for</param>
		/// <param name="valuesMustMatch">A match only takes place if the values of the element are equal</param>
		/// <param name="strictPathMatching">A match only occurs if the entire path is exactly the same. Otherwise, only the own name is important</param>
		/// <returns></returns>
		private static List<XmlInfoObject> FindXmlInfoObject(XDocument xmlDocument, XElement xmlElement, bool strictPathMatching, bool valuesMustMatch = true)
		{

			var foundElements = new List<XmlInfoObject>();

			// The minimum filter that is applied. More filtering may be applied within the loop
			foreach (XElement element in xmlDocument.Descendants().Where(x => x.Name.LocalName == xmlElement.Name.LocalName))
			{

				// Skip all elements which are not on the lowest level.
				if (element.HasElements) { continue; }

				// The value must not be compared for dynamic elements. However, the element must exist.
				if (valuesMustMatch && element.Value != xmlElement.Value) { continue; }

				// If the strict path comparison is activated, not only the name and the parent name 
				// are used for the path comparison, but the entire path.
				if (strictPathMatching && GetPathAsString(element) != GetPathAsString(xmlElement)) { continue; }

				// The object for internal use
				XmlInfoObject xmlInfoObject = new XmlInfoObject
				(
					xmlElement: element,
					isDynamic: DynamicXmlElements.Contains(element.Name.LocalName)
				);

				foundElements.Add(xmlInfoObject);

			}

			return foundElements;

		}
		
		/// <summary>
		/// Converts the elements of the xml file into the internal format for further processing
		/// </summary>
		/// <param name="xmlDocument">The xml that should be parsed</param>
		/// <returns>A list of the internal used xml Info object</returns>
		private static List<XmlInfoObject> XmlInfoObjectParser(XDocument xmlDocument)
		{

			List<XmlInfoObject> foundElements = new List<XmlInfoObject>();

			foreach (XElement elementToParse in xmlDocument.Descendants())
			{

				// elements with the ignored flag should be skipped
				if (IgnoredXmlElements.Any(e => e.ToLower() == elementToParse.Name.LocalName.ToLower()))
				{
					continue;				
				}

				// Elements with sub-objects are not parsed. However, the lowest sub-objects are.
				if (elementToParse.HasElements) 
				{
					LogDebug("XmlInfoObjectParser", string.Format("The element '{0}' has childs and will be ignored.", elementToParse.Name.LocalName));
					continue; 
				}

				// The flags in the XML are evaluated here
				// No value comparison should take place for a dynamic value. Only the existence of the element should be checked there
				// Ignored elements are simply not considered at all
				if (elementToParse.Value.ToLower() == "!dynamic")
				{
					AddDynamicXmlElement(elementToParse);				
				}

				if (elementToParse.Value.ToLower() == "!ignore")
				{					
					LogDebug("XmlInfoObjectParser", string.Format("The element '{0}' will bei ignored at all.", elementToParse.Name.LocalName));
					continue;
				}

				// Neues xml Infoobject erstellen
				XmlInfoObject xmlInfoObject = new XmlInfoObject
				(
					xmlElement: elementToParse,
					isDynamic: DynamicXmlElements.Contains(elementToParse.Name.LocalName)				
				);

				foundElements.Add(xmlInfoObject);

			}

			return foundElements;

		}

		/// <summary>
		/// Returns a path of the xml elements that is similar to a directory path.
		/// </summary>
		/// <param name="element">The element for which a path is to be created</param>
		/// <returns>A path as a string, which is helpful for identifying an xml element.</returns>
		private static string GetPathAsString(XElement element)
		{

			if (element == null)
			{
				throw new ArgumentNullException("the XElement element is null");
			}

			var allParents = GetAllParents(element);

			// Build the entire element path
			StringBuilder pathBuilder = new StringBuilder();
			foreach (XmlInfoObject xObject in allParents)
			{
				
				pathBuilder.Append(xObject.Name);

				// The last element should not get an slash.
				if (allParents.IndexOf(xObject) != allParents.Count - 1)
				{
					pathBuilder.Append('/');
				}

			}

			return pathBuilder.ToString();
		}

		/// <summary>
		/// Adds a comment to a xml file
		/// </summary>
		/// <param name="xmlFile">the file to comment</param>
		/// <param name="message">the comment message</param>
		private static void CommentXmlFile(XDocument xmlFile, string message)
		{

			if(xmlFile == null)
			{
				throw new ArgumentNullException("xmlFile is null");
			}

			// No message? No comment!
			if(string.IsNullOrEmpty(message)){return;}

			XComment xmlComment = new XComment(message);
			xmlFile.Add(xmlComment);

		}

		#endregion

		#region ReportFunctions

		/// <summary>
		/// Outputs a debug log if the switch for this is set to True
		/// </summary>
		/// <param name="category">The log category</param>
		/// <param name="message">The log message</param>
		private static void LogDebug(string category, string message)
		{
			// Es wird nur eine debug Nachricht ausgegeben, wenn der Schalter dafür auf True steht.
			if (!IsDebug) { return; }

			// Hier wird das Log ausgegeben.
			Report.Debug(category, message);

		}

				/// <summary>
		/// This is a placeholder text. Please describe the purpose of the
		/// user code method here. The method is published to the user code library
		/// within a user code collection.
		/// </summary>		
		private static void ReportTestStepResult(bool stepIsFailed, string reportMessage)
		{

			// To avoid unnecessarily inflating the report, we only output success messages for steps for debug purposes. 
			// Errors are of course always output
			if (IsDebug == false && stepIsFailed == false)
			{
				return;
			}

			Report.Log(level: stepIsFailed ? ReportLevel.Error : ReportLevel.Success,
					category: ReportCategory,
					message: reportMessage);

		}

		/// <summary>
		/// Constructs the heading for the test step in the Ranorex report 
		/// </summary>
		/// <param name="xmlInfoObject">The internal xml object that is evaluated</param>
		/// <param name="type">The type of the file (reference or test)</param>
		/// <returns>A heading line for a step in the Ranorex report</returns>		
		private static string LogBuilderHeader(XmlInfoObject xmlInfoObject, XFileType type)
		{

			if (xmlInfoObject == null)
			{
				throw new ArgumentNullException("xmlInfoObject was null");
			}

			switch (xmlInfoObject.XmlElement.Parent != null)
			{
				case true:
					return string.Format("{0} element: {1}/{2} ==> {3}",
										 type == XFileType.Reference ? "Reference" : "Test",
										 xmlInfoObject.XmlElement.Parent.Name.LocalName,
										 xmlInfoObject.Name,
										 xmlInfoObject.XmlElement.Value);

				case false:
					return string.Format("{0} element: {2} ==> {3}",
										 type == XFileType.Reference ? "Reference" : "Test",
										 xmlInfoObject.XmlElement.Parent.Name.LocalName,
										 xmlInfoObject.XmlElement.Value);

				default:
					// I think you cant reach this point
					return null;
			}

		}

		/// <summary>
		/// Builds the rows and path information for the Ranorex report
		/// </summary>		
		private static string LogBuilderLineInformation(XmlInfoObject xmlInfoObject)
		{

			return string.Format("Line: {0} ==> '{1}'",
								 xmlInfoObject.LineNumber != -1 ? xmlInfoObject.LineNumber.ToString() : "unknown",
								 GetPathAsString(xmlInfoObject.XmlElement));

		}

		/// <summary>
		/// Builds the information about the dynamic of the element for the Ranorex report
		/// </summary>
		/// <param name="xmlInfoObject">The element to be evaluated</param>
		/// <returns>A Report line about the dynamic informations about the element</returns>
		private static string LogBuilderIsDynamic(XmlInfoObject xmlInfoObject)
		{
			return string.Format("Is dynamic: {0}", xmlInfoObject.IsDynamic ? "Yes" : "No");
		}

		/// <summary>
		/// Returns the number of an element in the XML as report info.
		/// </summary>
		private static string LogBuilderElementCounter(int amountOf, XFileType fileType)
		{
			return string.Format("Frequency in the {0} file: {1}",
								 fileType == XFileType.Reference ? "reference" : "test",
								 amountOf.ToString());

		}

		/// <summary>
		/// This is a placeholder text. Please describe the purpose of the
		/// user code method here. The method is published to the user code library
		/// within a user code collection.
		/// </summary>
		private static string LogBuilderError01(int countTest, int countRef, XmlInfoObject xmlInfoObject, bool includeExistsNote)
		{

			StringBuilder logBuilder = new StringBuilder();

			logBuilder.AppendLine("==> Error 01");
			logBuilder.AppendLine(string.Format("The number of reference element '{0}' with value '{1}' in the test file differs from the reference file.",
												xmlInfoObject.Name,
												xmlInfoObject.XmlElement.Value));
			if (includeExistsNote)
			{
				logBuilder.AppendLine("There are reference elements with the same name but different values");
			}

			return logBuilder.ToString();

		}

		/// <summary>
		/// This is a placeholder text. Please describe the purpose of the
		/// user code method here. The method is published to the user code library
		/// within a user code collection.
		/// </summary>
		private static string LogBuilderError02(int countInTest, XmlInfoObject xmlInfoObject)
		{

			StringBuilder logBuilder = new StringBuilder();

			logBuilder.AppendLine("==> Error 02");
			logBuilder.AppendLine(string.Format("The test element '{0}' with value '{1}' is unknown for the reference file.",
												xmlInfoObject.Name,
												xmlInfoObject.XmlElement.Value));

			return logBuilder.ToString();

		}

		#endregion

		#region TestControl

		/// <summary>
		/// Starts the xml comparison test. 
		/// The reference and test file must be stored for this. It is best to use the load method and the corresponding helper methods for this.
		/// Note the configuration options.
		/// </summary>
		[UserCodeMethod]
		public static void StartTest()
		{
			// Here we validate the configurations
			if (ReferenceFile == null || TestFile == null)
			{
				throw new ArgumentNullException("The reference or test file is null");
			}

			if (SaveTestFileOnError && (string.IsNullOrEmpty(SaveTestFileName) || string.IsNullOrEmpty(SaveTestFilePath)))
			{
				Report.Warn(ReportCategory, "Your configuration is incorrect. The export function of the xml file is deactivated");
			}

			// These are our parsed elements that we need for all subsequent test steps
			var parsedReferenceElements = XmlInfoObjectParser(ReferenceFile);
			var parsedTestElements = XmlInfoObjectParser(TestFile);

			// We need these values to be able to output the correct log.
			bool isTestCaseFailed = false;
			bool isTestStepFailed = false;
			int teststepErrorCounter = 0;

			// PHASE 1 BEGINS HERE

			Report.Info(ReportCategory, "Phase 1 => All elements in the reference file are determined in the test file. " +
						"If there is a deviation in the frequency of the element in the test file, there is an error.");

			foreach (var parsedReferenceElement in parsedReferenceElements)
			{

				// reset the step error flag
				isTestStepFailed = false;

				// Hier wird die Anzahl des Elements in der Referenzdatei und in der Testdatei gezählt.
				// bei dynamischen Werten muss zumindest die selbe Anzahl an Elementen vorliegen, wenn der Wert ignoriert wird.
				var foundReferenceElements = FindXmlInfoObject(
					xmlDocument: ReferenceFile,
					xmlElement: parsedReferenceElement.XmlElement,
					valuesMustMatch: !parsedReferenceElement.IsDynamic,
					strictPathMatching: StrictPathMatching);

				var foundTestElements = FindXmlInfoObject(
					xmlDocument: TestFile,
					xmlElement: parsedReferenceElement.XmlElement,
					valuesMustMatch: !parsedReferenceElement.IsDynamic,
					strictPathMatching: StrictPathMatching);


				// The log is built here, which outputs the general information about the XML element.
				StringBuilder logBuilder = new StringBuilder();
				logBuilder.AppendLine(LogBuilderHeader(parsedReferenceElement, XFileType.Reference))
				.AppendLine(LogBuilderLineInformation(parsedReferenceElement))
				.AppendLine(LogBuilderIsDynamic(parsedReferenceElement))
				.AppendLine(LogBuilderElementCounter(foundReferenceElements.Count, XFileType.Reference))
				.AppendLine(LogBuilderElementCounter(foundTestElements.Count, XFileType.Test));

				// This is the entire validation step
				if (foundReferenceElements.Count != foundTestElements.Count)
				{

					isTestStepFailed = true;
					isTestCaseFailed = true;
					teststepErrorCounter = teststepErrorCounter + 1;

					bool identicalElementsExists = FindXmlInfoObject(xmlDocument: TestFile,
						xmlElement: parsedReferenceElement.XmlElement,
						strictPathMatching: StrictPathMatching,
						valuesMustMatch: false).Count != 0;

					logBuilder.AppendLine(LogBuilderError01(
						countTest: foundTestElements.Count,
						countRef: foundReferenceElements.Count,
						xmlInfoObject: parsedReferenceElement,
						includeExistsNote: identicalElementsExists));

				}

				// To comment the entire logmessage on the test xml file    			
				if (CommentTestFileOnError && isTestStepFailed)
				{
					CommentXmlFile(TestFile, logBuilder.ToString());
				}

				// Send the Report to the user (on failure or debug)
				ReportTestStepResult(isTestStepFailed, logBuilder.ToString());

			}

			Report.Log(level: teststepErrorCounter == 0 ? ReportLevel.Success : ReportLevel.Failure,
					category: ReportCategory,
					message: string.Format("Phase 1 completed. {0} reference elements were checked in the test file. {1} deviations were identified.",
									   		parsedReferenceElements.Count.ToString(),
									   		teststepErrorCounter.ToString()));

			// PHASE 2 BEGINS HERE

			// reset the error counter for the next phase 	
			teststepErrorCounter = 0;

			// Send log about the next testphase
			StringBuilder reportInfoBuilder = new StringBuilder();
			reportInfoBuilder.AppendLine("Phase 2 => All test elements are cross-checked in the reference file.");
			reportInfoBuilder.AppendLine("(If there are new elements in the test file that are completely unknown to the reference file, these could not be found in phase 1.)");

			Report.Info(ReportCategory, reportInfoBuilder.ToString());

			foreach (var testElement in parsedTestElements)
			{
				isTestStepFailed = false;

				int totalReferenceElements = FindXmlInfoObject(
					xmlDocument: ReferenceFile,
					xmlElement: testElement.XmlElement,
					valuesMustMatch: false,
					strictPathMatching: false).Count;

				int totalTestElements = FindXmlInfoObject(
					xmlDocument: TestFile,
					xmlElement: testElement.XmlElement,
					valuesMustMatch: false,
					strictPathMatching: false).Count;

				// The report information will be shorter in phase 2.
				StringBuilder logBuilder = new StringBuilder();
				logBuilder.AppendLine(LogBuilderHeader(testElement, XFileType.Test))
				.AppendLine(LogBuilderLineInformation(testElement))
				.AppendLine(LogBuilderElementCounter(totalReferenceElements, XFileType.Reference))
				.AppendLine(LogBuilderElementCounter(totalTestElements, XFileType.Test));

				if (totalReferenceElements == 0)
				{
					isTestStepFailed = true;
					teststepErrorCounter = teststepErrorCounter + 1;

					logBuilder.AppendLine(LogBuilderError02(totalTestElements, testElement));

				}

				ReportTestStepResult(stepIsFailed: isTestStepFailed, reportMessage: logBuilder.ToString());

				if (CommentTestFileOnError && isTestStepFailed)
				{
					CommentXmlFile(TestFile, logBuilder.ToString());
				}

			}

			Report.Log(
				level: teststepErrorCounter == 0 ? ReportLevel.Success : ReportLevel.Failure,
				category: ReportCategory,
				message: string.Format("Phase 2 completed. {0} test elements were checked in the reference file. {1} unknown elements were identified.",
									   parsedReferenceElements.Count.ToString(),
									   teststepErrorCounter.ToString()));

			// After Test tasks if test is failed
			if (isTestCaseFailed)
			{

				// This is a public readable property, to get the informations about the test result.
				IsTestFailed = true;

				// We can report the testfile, if the the option is true
				if (ReportTestfileOnError)
				{
					Report.Info(category: ReportCategory, message: TestFile.ToString());
				}

				// Writes the testfile to disk, if the option is true
				if (SaveTestFileOnError)
				{
					string filepath = System.IO.Path.Combine(SaveTestFilePath, SaveTestFileName);

					Report.Info(category: ReportCategory, message: string.Format("Write testfile to disk: {0}", filepath));

					WriteFileToDisk(xmlDocument: TestFile, path: filepath);
				}

			}

		}

		#endregion

		#region FilesystemFunctions

		/// <summary>
		/// Writes a xml file to disk.
		/// Creates the dictionary if neccesary. Overwrites existing files.
		/// </summary>
		/// <param name="xmlDocument">the xml file you want to save</param>
		/// <param name="path">the file path</param>
		[UserCodeMethod]
		public static void WriteFileToDisk(XDocument xmlDocument, string path)
		{

			if (xmlDocument == null)
			{
				throw new ArgumentNullException("xmlDocument is null");
			}
			if (string.IsNullOrEmpty(path))
			{
				throw new ArgumentNullException("path is null");
			}

			string dictionary = System.IO.Path.GetDirectoryName(path);
			if (!System.IO.Directory.Exists(dictionary))
			{
				System.IO.Directory.CreateDirectory(dictionary);
			}

			xmlDocument.Save(path);

		}

		/// <summary>
		/// Returns whether the specified file is a supported file
		/// </summary>
		/// <param name="file">The file path</param>
		/// <returns>True if the file is supported</returns>
		private static bool IsSupportedFile(string file)
		{

			if (file == null)
			{
				throw new ArgumentNullException("file is null");
			}

			string fileExtension = System.IO.Path.GetExtension(file);

			return SupportedFileTypes.Any(f => f.Replace(".", "").ToLower() == fileExtension.Replace(".", "").ToLower());

		}

		/// <summary>
		/// Removes all files from a directory that contains the supported file type 
		/// from the SupportedFileExtension list
		/// </summary>
		[UserCodeMethod]
		public static void ClearDictionary(string path)
		{

			if (path == null)
			{
				throw new ArgumentNullException("path is null");
			}

			if (!System.IO.Directory.Exists(path))
			{
				Report.Warn(ReportCategory, string.Format("The specified directory does not exist yet: {0}", path));
				return;
			}

			var supportedFiles = GetSupportedFilesFromPath(path);

			foreach(var file in supportedFiles)
			{

				try
				{
					System.IO.File.Delete(file);
					LogDebug("ClearDictionary",string.Format("The file was deleted: {0}",file));
				}
				catch(Exception ex)
				{
					Report.Failure(ReportCategory,string.Format("Could not delete file {0}. Exception: {1}",file,ex.Message));
				}			

			}

		}

		/// <summary>
		/// Returns all supported files from a directory.
		/// Supported files are files whose file associations are contained in the corresponding list
		/// </summary>
		/// <param name="path">The directory in which the files are located</param>
		/// <returns>A list of paths to the supported files found in the directory</returns>
		private static List<string> GetSupportedFilesFromPath(string path)
		{

			if (path == null)
			{
				throw new ArgumentNullException("path is null");
			}

			var supportedFiles = System.IO.Directory.GetFiles(path).Where(f => IsSupportedFile(f)).ToList();

			LogDebug("GetSupportedFilesFromPath", string.Format("{0} supported files were found in {1}.",
																supportedFiles.Count.ToString(), path));

			return supportedFiles;

		}

		/// <summary>
		/// Loads an xml file from the file system and assigns it to the property (test or reference file).
		/// </summary>
		/// <param name="path">The path to the xml file</param>
		/// <param name="type">The information whether it is the reference or test file</param>
		[UserCodeMethod]
		public static void LoadFile(string path, XFileType type)
		{

			if (path == null)
			{
				throw new ArgumentNullException("path is null");
			}

			if (!System.IO.File.Exists(path))
			{
				Report.Failure(ReportCategory, string.Format("The file does not exists: {0}", path));
				return;
			}

			// Sets the needed xml file
			switch (type)
			{
				case XmlValidation.XFileType.Reference:
					ReferenceFile = XDocument.Load(path, LoadOptions.SetLineInfo);
					Report.Success(ReportCategory, string.Format("The reference file has been loaded: {0}", path));
					break;
				
				case XmlValidation.XFileType.Test:
					TestFile = XDocument.Load(path, LoadOptions.SetLineInfo);
					Report.Success(ReportCategory, string.Format("The test file has been loaded: {0}", path));
					SaveTestFileName = System.IO.Path.GetFileName(path);
					break;
				
				default:
					throw new Exception("Invalid value for XFileType");
			}

		}

		#endregion

		public class XmlInfoObject
		{
			public string Name { get; set; }
			public XElement XmlElement { get; set; }
			public int LineNumber { get; set; }
			public bool IsDynamic { get; set; }

			public XmlInfoObject(XElement xmlElement, bool isDynamic)
			{

				if (xmlElement == null) { throw new ArgumentNullException("Es muss ein XElement mitgeliefert werden"); }


				Name = xmlElement.Name.LocalName;
				XmlElement = xmlElement;
				IsDynamic = isDynamic;

				IXmlLineInfo lineInfo = xmlElement;
				if (lineInfo.HasLineInfo())
				{
					LineNumber = lineInfo.LineNumber;
				}
				else
				{
					LineNumber = -1;
				}
			}



		}




	}
}
