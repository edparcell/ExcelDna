/*
  Copyright (C) 2005-2010 Govert van Drimmelen

  This software is provided 'as-is', without any express or implied
  warranty.  In no event will the authors be held liable for any damages
  arising from the use of this software.

  Permission is granted to anyone to use this software for any purpose,
  including commercial applications, and to alter it and redistribute it
  freely, subject to the following restrictions:

  1. The origin of this software must not be misrepresented; you must not
     claim that you wrote the original software. If you use this software
     in a product, an acknowledgment in the product documentation would be
     appreciated but is not required.
  2. Altered source versions must be plainly marked as such, and must not be
     misrepresented as being the original software.
  3. This notice may not be removed or altered from any source distribution.


  Govert van Drimmelen
  govert@icon.co.za
*/

using System; 
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Xml.Serialization;
using System.Xml;
using System.Xml.Schema;
using ExcelDna.Integration.CustomUI;
using System.Drawing;

namespace ExcelDna.Integration
{
	[Serializable]
	[XmlType(AnonymousType = true)]
	[XmlRoot(Namespace = "", IsNullable = false)]
	public class DnaLibrary
	{
		private List<ExternalLibrary> _ExternalLibraries;
		[XmlElement("ExternalLibrary", typeof(ExternalLibrary))]
        public List<ExternalLibrary> ExternalLibraries
		{
			get { return _ExternalLibraries; }
			set	{ _ExternalLibraries = value; }
		}

		private List<Project> _Projects;
		[XmlElement("Project", typeof(Project))]
        public List<Project> Projects
		{
			get { return _Projects; }
			set { _Projects = value; }
		}

        private static string _XllPath = null;
        [XmlIgnore]
        internal static string XllPath
        {
            get
            {
                if (_XllPath == null)
                {
                    _XllPath = (string)XlCall.Excel(XlCall.xlGetName);
                }
                return _XllPath;
            }
        }

		private string _Name;
		[XmlAttribute]
		public string Name
		{
			get
			{
				if (_Name == null || _Name == "")
				{
					_Name = Path.GetFileNameWithoutExtension(_XllPath);
				}
				return _Name;
			}
			set { _Name = value; }
		}

        private string _RuntimeVersion; // default is effectively v2.0.50727 ?
        [XmlAttribute]
        public string RuntimeVersion
        {
            get { return _RuntimeVersion; }
            set { _RuntimeVersion = value; }
        }

        // Next bunch are abbreviations for Project contents
        private List<Reference> _References;
        [XmlElement("Reference", typeof(Reference))]
        public List<Reference> References
        {
            get { return _References; }
            set { _References = value; }
        }

        private string _Language; // Default is VB
        [XmlAttribute]
        public string Language
        {
            get { return _Language; }
            set { _Language = value; }
        }

		private string _CompilerVersion;
		[XmlAttribute]
		public string CompilerVersion
		{
			get { return _CompilerVersion; }
			set { _CompilerVersion = value; }
		}

        private bool _DefaultReferences = true;
        [XmlAttribute]
        public bool DefaultReferences
        {
            get { return _DefaultReferences; }
            set { _DefaultReferences = value; }
        }

        private bool _DefaultImports = true;
        [XmlAttribute]
        public bool DefaultImports
        {
            get { return _DefaultImports; }
            set { _DefaultImports = value; }
        }

		// No ExplicitExports flag on the DnaLibrary (for now), because it might cause confusion when mixed with ExternalLibraries.
		// Projects can be marked as ExplicitExports by adding an explicit <Project> tag.
		//private bool _ExplicitExports = false;
		//[XmlAttribute]
		//public bool ExplicitExports
		//{
		//    get { return _ExplicitExports; }
		//    set { _ExplicitExports = value; }
		//}
        
        private string _Code;
        [XmlText]
        public string Code
        {
            get { return _Code; }
            set { _Code = value; }
        }

 
        // DOCUMENT: The three different elements are their namespaces.
        //           The CustomUI and inner customUI are case sensitive.
        private List<XmlNode> _CustomUIs;
        [XmlElement("CustomUI", typeof(XmlNode))]
        public List<XmlNode> CustomUIs
        {
            get { return _CustomUIs; }
            set { _CustomUIs = value; }
        }

        private List<Image> _Images;
        [XmlElement("Image", typeof(Image))]
        public List<Image> Images
        {
            get { return _Images; }
            set { _Images = value; }
        }

        private string dnaResolveRoot;

        // Get projects explicit and implicitly present in the library
        public List<Project> GetProjects()
        {
            List<Project> projects = new List<Project>();
            if (Projects != null)
                projects.AddRange(Projects);
            if (_Code != null && Code.Trim() != "")
                projects.Add(new Project(Language, CompilerVersion, References, _Code, DefaultReferences, DefaultImports, false));
            return projects;
        }

		internal List<ExportedAssembly> GetAssemblies(string pathResolveRoot)
		{
            List<ExportedAssembly> assemblies = new List<ExportedAssembly>();
			try
			{
				if (ExternalLibraries != null)
				{
					foreach (ExternalLibrary lib in ExternalLibraries)
					{
                        assemblies.AddRange(lib.GetAssemblies(pathResolveRoot));
					}
				}
				foreach (Project proj in GetProjects())
				{
                    assemblies.AddRange(proj.GetAssemblies(pathResolveRoot));
				}
			}
			catch (Exception e)
			{
				Logging.LogDisplay.WriteLine("Error while loading assemblies. Exception: " + e.Message);
			}
			return assemblies;
		}

        // Managed AddIns related to this DnaLibrary
        // Kept so that AutoClose can be called later.
        [XmlIgnore]
        private List<AssemblyLoader.ExcelAddInInfo> _AddIns = new List<AssemblyLoader.ExcelAddInInfo>();

        internal void AutoOpen()
        {
            List<MethodInfo> methods = new List<MethodInfo>();

            // Get MethodsInfos and AddIn classes from assemblies
            foreach (ExportedAssembly assembly in GetAssemblies(dnaResolveRoot))
            {
                try
                {
                    methods.AddRange(AssemblyLoader.GetExcelMethods(assembly));
                }
                catch (Exception e)
                {
                    // TODO: I still don't know how to do exceptions
                    Debug.WriteLine(e.Message);
                }
                _AddIns.AddRange(AssemblyLoader.GetExcelAddIns(assembly));

                // Register RTD Server Types
                Dictionary<String, Type> rtdServerTypes = AssemblyLoader.GetRtdServerTypes(assembly);
                Rtd.ExcelRtd.RegisterRtdServerTypes(rtdServerTypes);
            }

            // Register Methods
            Integration.RegisterMethods(methods);

            // Invoke AutoOpen in all assemblies
            foreach (AssemblyLoader.ExcelAddInInfo addIn in _AddIns)
            {
                try
                {
                    if (addIn.AutoOpenMethod != null)
                    {
                        addIn.AutoOpenMethod.Invoke(addIn.Instance, null);
                    }
                }
                catch (Exception e)
                {
                    // TODO: What to do here?
                    Debug.Print(e.Message);
                }
            }

            LoadCustomUI();
        }

        internal void AutoClose()
        {
            UnloadCustomUI();

            foreach (AssemblyLoader.ExcelAddInInfo addIn in _AddIns)
            {
                try
                {
                    if (addIn.AutoCloseMethod != null)
                    {
                        addIn.AutoCloseMethod.Invoke(addIn.Instance, null);
                    }
                }
                catch (Exception e)
                {
                    // TODO: What to do here?
                    Debug.WriteLine(e.Message);
                }
            }
            _AddIns.Clear();
        }

        internal void LoadCustomUI()
        {
            bool uiLoaded = false;
            if (ExcelDnaUtil.ExcelVersion >= 12.0)
            {
                // Load ComAddIns
                foreach (AssemblyLoader.ExcelAddInInfo addIn in _AddIns)
                {
                    if (addIn.IsCustomUI)
                    {
                        // Load ExcelRibbon classes
                        ExcelRibbon excelRibbon = addIn.Instance as ExcelRibbon;
                        excelRibbon.DnaLibrary = this;
                        ExcelComAddIn.LoadComAddIn(excelRibbon);
                        uiLoaded = true;
                    }
                }

                if (uiLoaded == false && CustomUIs != null)
                {
                    // Check whether we should add an empty ExcelCustomUI instance to load a Ribbon interface?
                    bool loadEmptyAddIn = false;
                    if (CustomUIs != null)
                    {
                        foreach (XmlNode xmlCustomUI in CustomUIs)
                        {
                            if (xmlCustomUI.LocalName == "customUI" &&
                                (xmlCustomUI.NamespaceURI == ExcelRibbon.NamespaceCustomUI2007 ||
                                 (ExcelDnaUtil.ExcelVersion >= 14.0 &&
                                  xmlCustomUI.NamespaceURI == ExcelRibbon.NamespaceCustomUI2010)))
                            {
                                loadEmptyAddIn = true;
                            }
                            if (loadEmptyAddIn)
                            {
                                // There will be Ribbon xml to load. Make a temp add-in and load it.
                                ExcelRibbon customUI = new ExcelRibbon();
                                customUI.DnaLibrary = this;
                                ExcelComAddIn.LoadComAddIn(customUI);
                                uiLoaded = true;
                            }
                        }
                    }
                }
            }

            // should we load CommandBars?
            if (uiLoaded == false && CustomUIs != null)
            {
                foreach (XmlNode xmlCustomUI in CustomUIs)
                {
                    if (xmlCustomUI.LocalName == "commandBars")
                    {
                        ExcelCommandBars.LoadCommandBars(xmlCustomUI, this);
                    }
                }
            }
        }

        internal void UnloadCustomUI()
        {
            // This is safe, even if no Com Add-Ins were loaded.
            ExcelComAddIn.UnloadComAddIns();
            ExcelCommandBars.UnloadCommandBars();
        }

        // Statics
		private static DnaLibrary currentLibrary;
		internal static void Initialize()
		{
            // Might be called more than once in a session
            // e.g. if AddInManagerInfo is called, and then AutoOpen
            // or if the add-in is opened more than once.

			// Loads the primary .dna library
			// Load sequence is:
			// 1. Look for a packed .dna file named "__MAIN__" in the .xll.
			// 2. Look for the .dna file in the same directory as the .xll file, with the same name and extension .dna.

			Debug.WriteLine("Enter DnaLibrary.Initialize");
            Logging.LogDisplay.CreateInstance();
			byte[] dnaBytes = Integration.GetDnaFileBytes("__MAIN__");
			if (dnaBytes != null)
			{
				Debug.WriteLine("Got Dna file from resources.");
                string pathResolveRoot = Path.GetDirectoryName(DnaLibrary.XllPath);
				currentLibrary = LoadFrom(dnaBytes, pathResolveRoot);
				// ... would have displayed error and returned null if there was an error.
			}
			else
			{
				Debug.WriteLine("No Dna file in resources - looking for file.");
				// No packed .dna file found - load from a .dna file.
				string dnaFileName = Path.ChangeExtension(XllPath, ".dna");
				currentLibrary = LoadFrom(dnaFileName);
				// ... would have displayed error and returned null if there was an error.
			}

			// If there have been problems, ensure that there is at lease some current library.
			if (currentLibrary == null)
			{
				Debug.WriteLine("No Dna Library found.");
				currentLibrary = new DnaLibrary();
			}
			Debug.WriteLine("Exit DnaLibrary.Initialize");
		}

        internal static void DeInitialize()
        {
            // Called to shut down the Add-In.
            // Free whatever possible
            currentLibrary = null;
        }

		public static DnaLibrary LoadFrom(byte[] bytes, string pathResolveRoot)
		{
			DnaLibrary dnaLibrary;
			XmlSerializer serializer = new Microsoft.Xml.Serialization.GeneratedAssembly.DnaLibrarySerializer();

			try
			{
				using (MemoryStream ms = new MemoryStream(bytes))
				{
					dnaLibrary = (DnaLibrary)serializer.Deserialize(ms);
				}
			}
			catch (Exception e)
			{
				string errorMessage = string.Format("There was an error while processing DnaFile bytes:\r\n{0}\r\n{1}", e.Message, e.InnerException != null ? e.InnerException.Message : string.Empty);
				Debug.WriteLine(errorMessage);
				//ExcelDna.Logging.LogDisplay.SetText(errorMessage);
				return null;
			}
            dnaLibrary.dnaResolveRoot = pathResolveRoot;
			return dnaLibrary;
		}

        public static DnaLibrary LoadFrom(string fileName)
        {
            DnaLibrary dnaLibrary;

			if (!File.Exists(fileName))
			{
				ExcelDna.Logging.LogDisplay.WriteLine("The required .dna script file {0} does not exist.", fileName);
				return null;
			}

            try
            {
				XmlSerializer serializer = new Microsoft.Xml.Serialization.GeneratedAssembly.DnaLibrarySerializer();
				using (FileStream fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read))
                {
                    dnaLibrary = (DnaLibrary)serializer.Deserialize(fileStream);
                }
            }
            catch (Exception e)
            {
                ExcelDna.Logging.LogDisplay.WriteLine("There was an error while processing {0}:\r\n{1}\r\n{2}", fileName, e.Message, e.InnerException != null ? e.InnerException.Message : string.Empty);
                return null;
            }
            dnaLibrary.dnaResolveRoot = Path.GetDirectoryName(fileName);
            return dnaLibrary;
        }

		public static void Save(string fileName, DnaLibrary dnaLibrary)
		{
//			XmlSerializer serializer = new XmlSerializer(typeof(DnaLibrary));
			XmlSerializer serializer = new Microsoft.Xml.Serialization.GeneratedAssembly.DnaLibrarySerializer(); 
			using (FileStream fileStream = new FileStream(fileName, FileMode.Truncate))
			{
				serializer.Serialize(fileStream, dnaLibrary);
			}
		}

		public static byte[] Save(DnaLibrary dnaLibrary)
		{
			//			XmlSerializer serializer = new XmlSerializer(typeof(DnaLibrary));
			XmlSerializer serializer = new Microsoft.Xml.Serialization.GeneratedAssembly.DnaLibrarySerializer();
			using (MemoryStream ms = new MemoryStream())
			{
				serializer.Serialize(ms, dnaLibrary);
				return ms.ToArray();
			}
		}

		public static DnaLibrary CurrentLibrary
		{
            // Might be called before Initialize
            // e.g. if Excel called AddInManagerInfo before AutoOpen
			get
			{
                if (currentLibrary == null)
                    Initialize();
				return currentLibrary;
			}
		}

        // Called during initialize when displaying log
        // TODO: Clean up - inserted to prevent recursion when displaying log during initialize
        internal static string CurrentLibraryName
        {
            get
            {
                if (currentLibrary == null) 
                {
                    string dllName = XllPath;
					return Path.GetFileNameWithoutExtension(dllName);
				}
                return CurrentLibrary.Name;
            }
        }

        internal static string ExecutingDirectory
        {
            get
            {
                return Path.GetDirectoryName(XllPath);
            }
        }

        public string ResolvePath(string path)
        {
            return ResolvePath(path, dnaResolveRoot);
        }

        // ResolvePath tries to figure out the actual path to a file - either a .dna file or an 
        // assembly to be packed.
        // Resolution sequence:
        // 1. Check the path - if not rooted that will be relative to working directory.
        // 2. If the path is rooted, try the filename part relative to the .dna file location.
        //    If the path is not rooted, try the whole path relative to the .dna file location.
        // 3. Try step 2 against the appdomain.
        public static string ResolvePath(string path, string dnaDirectory)
        {
            Debug.Print("ResolvePath: Resolving {0} from DnaDirectory: {1}", path, dnaDirectory);
            if (File.Exists(path))
            {
                Debug.Print("ResolvePath: Found at {0}", path);
                return path;
            }

            // Try relative to dna directory
            string dnaPath;
            if (Path.IsPathRooted(path))
            {
                // It was a rooted path -- try locally instead
                string fileName = Path.GetFileName(path);
                dnaPath = Path.Combine(dnaDirectory, fileName);
            }
            else
            {
                // Not rooted - try a path relative to local directory
                dnaPath = System.IO.Path.Combine(dnaDirectory, path);
            }
            Debug.Print("ResolvePath: Checking at {0}", dnaPath);
            if (File.Exists(dnaPath))
            {
                Debug.Print("ResolvePath: Found at {0}", dnaPath);
                return dnaPath;
            }

            // try relative to AppDomain BaseDirectory
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string basePath;
            if (Path.IsPathRooted(path))
            {
                string fileName = Path.GetFileName(path);
                basePath = Path.Combine(baseDirectory, fileName);
            }
            else
            {
                basePath = System.IO.Path.Combine(baseDirectory, path);
            }

            // Check again
            Debug.Print("ResolvePath: Checking at {0}", basePath);
            if (File.Exists(basePath))
            {
                Debug.Print("ResolvePath: Found at {0}", basePath);
                return basePath;
            }

            // Else give up (maybe try GAC?)
            Debug.Print("ResolvePath: Could not find {0} from DnaDirectory {1}", path, dnaDirectory);
            return null;
        }

        public Bitmap GetImage(string imageId)
        {
            // We expect these to be small images.

            // First check if imageId is in the DnaLibrary's Image list.
            // DOCUMENT: Case sensitive match.
            foreach (Image image in Images)
            {
                if (image.Name == imageId && image.Path != null)
                {
                    byte[] imageBytes = null;
                    System.Drawing.Image imageLoaded;
                    if (image.Path.StartsWith("packed:"))
                    {
                        string resourceName = image.Path.Substring(7);
                        imageBytes = Integration.GetImageBytes(resourceName);
                    }
                    else
                    {
                        string imagePath = ResolvePath(image.Path);
                        if (imagePath == null)
                        {
                            // This is the image but we could not find it !?
                            Debug.Print("DnaLibrary.LoadImage - For image {0} the path resolution failed: {1}", image.Name, image.Path);
                            return null;
                        }
                        imageBytes = File.ReadAllBytes(imagePath);
                    }
                    using (MemoryStream ms = new MemoryStream(imageBytes, false))
                    {
                        imageLoaded = Bitmap.FromStream(ms);
                        if (imageLoaded is Bitmap)
                        {
                            return (Bitmap)imageLoaded;
                        }
                        Debug.Print("Image {0} read from {1} was not a bitmap!?", image.Name, image.Path);
                    }
                }
            }
            return null;
        }

    }

    //public class CustomUI : IXmlSerializable
    //{
    //    public string Content { get; set; }
    //    public string NsUri { get; set; }


    //    public XmlSchema GetSchema()
    //    {
    //        return null;
    //    }

    //    public void WriteXml(XmlWriter w)
    //    {
    //        w.WriteStartElement("customUI");
    //        w.WriteAttributeString("xmlns", NsUri);
    //        w.WriteRaw(Content);
    //        w.WriteEndElement();
    //    }

    //    public void ReadXml(XmlReader r)
    //    {
    //        NsUri = r.NamespaceURI;
    //        Content = r.ReadOuterXml();
    //    }
    //
    //}
}


/*
public class CDataField : IXmlSerializable
    {
        private string elementName;
        private string elementValue;

        public CDataField(string elementName, string elementValue)
        {
            this.elementName = elementName;
            this.elementValue = elementValue;
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        public void WriteXml(XmlWriter w)
        {
            w.WriteStartElement(this.elementName);
            w.WriteCData(this.elementValue);
            w.WriteEndElement();
        }

        public void ReadXml(XmlReader r)
        {                      
            throw new NotImplementedException("This method has not been implemented");
        }
    }
 */ 
 
// CAREFUL: Customization of the deserialization in this if clause....
//else if (((object)Reader.LocalName == (object)id11_customUI /*&& (object)Reader.NamespaceURI == (object)id2_Item */))
//{
//    a_9.Add((global::System.Xml.XmlNode)ReadXmlNode(false /*true*/));
//}

