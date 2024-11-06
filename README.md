# MyRanorexToolbox
Hello,

I think the Ranorex community could be a bit bigger, so I would like to do my part.
In this repository I would like to provide all kinds of code snippets that have made my life a little easier. Maybe you will also benefit from it.

# Ranorex xml validation

I faced the problem that a software exported XML files that were always identical in content but structured in different orders, making a direct line-by-line comparison impossible. I needed a simple method to check if the elements with the correct values were present without considering namespaces or the position of the elements in the document. A "soft" validation was required, focusing only on the elements and their values.

The code in ./XmlValidation was developed to compare two XML files: a reference file that serves as the error-free template and a test file, such as one exported by the AUT (Application Under Test).

## Functionality

The code checks if all XML elements from the reference file are also present in the test file. This comparison uses the name and value of the elements. Elements with dynamic values, such as UUIDs or timestamps, can be marked with a special flag so that their value is ignored during the comparison.
The code then checks if there are elements in the test file that are unknown to the reference file, ensuring that no new, incorrect elements have been added.
Error Handling: In the event of discrepancies, detailed reports are generated to allow for precise analysis of the differences.

The code includes various settings to make it more adaptable to different requirements.

### Functionality example
#### Reference file
```xml
<?xml version="1.0" encoding="UTF-8"?>
  <root>
      <user>
          <id>1</id>
          <name>John Doe</name>
          <email>john.doe@example.com</email>
          <phone>+49 111 12345678</phone>
      </user>
      <user>
          <id>2</id>
          <name>Jane Smith</name>
          <email>jane.smith@example.com</email>
      </user>
  </root>
```

#### Test file
```xml
<?xml version="1.0" encoding="UTF-8"?>
  <root>
      <user>          
          <name>Jane Smith</name>
          <id>2</id>
          <email>jane.smith@example.com</email>
      </user>
      <user>
          <id>0</id>
          <name>John Bulldoezer</name>
          <email>john.doe@example.com</email>
          <gender>male</gender>
      </user>
  </root>
```

As we can see, the two files differ slightly in terms of content and structure.<br>
+ The program would detect the value of 'id' and 'name' of Jon Doe in the test file as incorrect because the value differs from the reference file.
+ The program would detect the missing value 'phone' of Jon Doe in the test file as incorrect because it exists in the reference file.
+ The program would find fault with the value 'gender' of Jon Doe in the test file, as it is not listed in the reference file.
+ The program would not find fault with any values of Jane Smith, although they are swapped in the order

## Code examples
These are our example xml files for all the examples mentioned.

### Example 1: Simplest example
We know the path to the reference and test file and do not want any other configuration
```csharp
public static void Example1()
{      	

  // In this use case we know the file paths
  string testFile = @"C:\example\test.xml";
  string referenceFile = @"C:\example\reference.xml";

  // We use the enum XFileType to determine exactly which file
  // we want to read in (reference or test file)
  // Loads the test file
  XmlValidation.LoadFile(testFile,XmlValidation.XFileType.Test);
        	
  // Loads the reference file
  XmlValidation.LoadFile(referenceFile,XmlValidation.XFileType.Reference);
        	
  // Starts the test
  XmlValidation.StartTest();

}
```
### Example 2: Unknown name of the test file
If, like me, you only know the directory in which the test file is created, but the name is unknown, there are two built-in options.

#### Option 1: The only existing xml file
We take the only xml file available in the directory.
(It is best to empty the directory before exporting the test file to it).

``` csharp
public void Example2a()
{

  // This line of code should be executed before the AUT 
  // exports the test file so that the directory is really empty.
  XmlValidation.ClearDictionary("C:\example");

  // .... The AUT exports the xml file ...

  // Gets the only supported file from the filesystem.
  string testFile = XmlValidation.GetSingleFileFromPath(@"C:\example\",true);
  string referenceFile = @"C:\reference files\reference.xml";        	

  // Loads the test file
  XmlValidation.LoadFile(testFile,XmlValidation.XFileType.Test);

  // Loads the reference file
  XmlValidation.LoadFile(referenceFile,XmlValidation.XFileType.Reference);

  // Starts the test
  XmlValidation.StartTest();

}
```

#### Option 2: Filter out the correct xml file
We have several xml files in the directory and cannot delete any of them. 
However, we know some elements that must be contained in the file.

```csharp
public void Example2b()
{

  // We specify elements that must be contained in the file you are looking for. 		
  // If a file contains all the specified values, it is returned.
  List<XElement> myReferenceElements = new List<XElement>()
  {	
    new XElement("name","John Doe"),
    new XElement("id","1")
    // ... more => better
  };			
  string testFile = XmlValidation.GetFirstMatchOrNull(@"C:\example",myReferenceElements);       	
					 	        	
  // Loads the test file
  string referenceFile = @"C:\example\reference.xml";       
  XmlValidation.LoadFile(testFile,XmlValidation.XFileType.Test);
        	
  // Loads the reference file
  XmlValidation.LoadFile(referenceFile,XmlValidation.XFileType.Reference);
        	
  // Starts the test
  XmlValidation.StartTest();
     	
}
```
### Example 3: configuration options
Configuration options with the program code are shown here.
The description of the configuration can be found in the code comments

```csharp
public void Example3()
{

  // You know this part already...
  string testFile = @"C:\example\test.xml";
  string referenceFile = @"C:\example\reference.xml";
  XmlValidation.LoadFile(testFile,XmlValidation.XFileType.Test);
  XmlValidation.LoadFile(referenceFile,XmlValidation.XFileType.Reference);

  // Let's configure something in the program

  // Loads the reference file
  XmlValidation.LoadFile(referenceFile,XmlValidation.XFileType.Reference);
      	
  // Set this Property to true, if you want signifivant more 
  // information in the Ranorex Report. Could help you with troubleshooting or give
  // you a better test feeling, as it also outputs the success message of individual test steps.
  XmlValidation.IsDebug = true;
        	
  // If you want the test file to be exported to a file if the test fails, set this value to True.
  // This could be helpful if the directory with the test files is cleaned up regularly.
  // However, a path must then also be specified.
  XmlValidation.SaveTestFileOnError = true;
  XmlValidation.SaveTestFilePath = @"C:\failure";
        	
  // We want the test file to be output in the report in the event of a failure.
  // (This is useful if you don't want to rummage through files)
  XmlValidation.ReportTestfileOnError = true;

  // Adds the log entry at the end of the test file in case  of an error.
  // Is practical in combination with ReportTestfileOnError or SaveTestFileOnError
  XmlValidation.CommentTestFileOnError = true;

  // If the program searches for an XElement in a file, the entire path of the element,
  // as well as the name and possibly the value, are used for the comparison.
  // If this switch is set to False, the exact path comparison is not carried out.
  // (Makes the test less accurate)
  public static bool StrictPathMatching = false;
      	
  // We have a dynamic element and want to exclude 
  // it from the exact value comparison.
  XElement myDynamicElement = new XElement("Uuid");
  XmlValidation.AddDynamicXmlElement(myDynamicElement);

  // Some elements doesn't matter at all and we can ignore them for the test.
  XElement myIgnoredElement = new XElement("other");
  XmlValidation.AddIgnoredXmlElement(myIgnoredElement);
        	
  // Starts the test
  XmlValidation.StartTest();

}
```

### Example 4: Dynamic or ignorable xml Elements
Dynamic elements (uuids, timestamps etc.) can be added on the code side as in example 3. However, we can also do this via the reference file.

There are two flags that we can set:
+ <b>!Dynamic</b><br>
  Elements with this flag are validated for existence without comparing values.<br> 
  Example: If there are two elements 'uuid' in the reference file, they must also exist twice in the test file. The values may differ.
+ <b>!Ignore:</b><br>
  Elements with this flag are skipped in the test and not taken into account in any way.

```xml
<?xml version="1.0" encoding="UTF-8"?>
<root>
  <user>        
    <id>0</id>
    <email>john.doe@example.com</email>
    <phone>+49 123 456789</phone>
    <name>John Doe</name>
    <!-- Set the flags as element value -->
    <uuid>!Dynamic</uuid>
    <other>!Ignore</other>
  </user>
  <user>
    <id>2</id>        
    <email>jane.smith@example.net</email>
    <name>Jane Smith</name>
    </user>
</root>
```
