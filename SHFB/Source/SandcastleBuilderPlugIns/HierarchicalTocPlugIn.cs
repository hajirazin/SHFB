//===============================================================================================================
// System  : Sandcastle Help File Builder Plug-Ins
// File    : HierarchicalTocPlugIn.cs
// Author  : Eric Woodruff  (Eric@EWoodruff.us)
// Updated : 05/16/2014
// Note    : Copyright 2008-2014, Eric Woodruff, All rights reserved
// Compiler: Microsoft Visual C#
//
// This file contains a plug-in that can be used to rearrange the table of contents such that namespaces are
// nested within their parent namespaces rather than appearing as a flat list of all namespaces at the root
// level.
//
// This code is published under the Microsoft Public License (Ms-PL).  A copy of the license should be
// distributed with the code.  It can also be found at the project website: https://GitHub.com/EWSoftware/SHFB.  This
// notice, the author's name, and all copyright notices must remain intact in all applications, documentation,
// and source files.
//
// Version     Date     Who  Comments
// ==============================================================================================================
// 1.6.0.6  03/17/2008  EFW  Created the code
// 1.8.0.0  07/22/2008  EFW  Fixed bug caused by root namespace container
// 1.9.0.0  06/22/2010  EFW  Suppressed use in MS Help Viewer output due to the way the TOC is generated in
//                           those files.
// 1.9.9.0  12/14/2013  EFW  Deprecated.  Use the namespace grouping project options instead.
// -------  12/17/2013  EFW  Updated to use MEF for the plug-ins
//===============================================================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml;
using System.Xml.XPath;

using Sandcastle.Core;

using SandcastleBuilder.Utils;
using SandcastleBuilder.Utils.BuildComponent;
using SandcastleBuilder.Utils.BuildEngine;

namespace SandcastleBuilder.PlugIns
{
    /// <summary>
    /// This plug-in class can be used to rearrange the table of contents such that namespaces are nested within
    /// their parent namespaces rather than appearing as a flat list of all namespaces at the root level.
    /// </summary>
    /// <remarks>This plug-in has been deprecated.  Use the namespace grouping project options instead.</remarks>
    [HelpFileBuilderPlugInExport("Hierarchical Table of Contents", IsConfigurable = true,
      Version = AssemblyInfo.ProductVersion, Copyright = AssemblyInfo.Copyright,
      Description = "This plug-in can be used to rearrange the table of contents such that namespaces are " +
        "nested within their parent namespaces rather than appearing as a flat list of all namespaces at the " +
        "root level.\r\n\r\nNOTE:  This plug-in has been deprecated.  Use the NamespaceGrouping and " +
        "MaximumGroupParts project options instead.")]
    public sealed class HierarchicalTocPlugIn : IPlugIn
    {
        #region Private data members
        //=====================================================================

        private List<ExecutionPoint> executionPoints;

        private BuildProcess builder;

        private int minParts;
        private bool insertBelow;
        #endregion

        #region IPlugIn implementation
        //=====================================================================

        /// <summary>
        /// This read-only property returns a collection of execution points that define when the plug-in should
        /// be invoked during the build process.
        /// </summary>
        public IEnumerable<ExecutionPoint> ExecutionPoints
        {
            get
            {
                if(executionPoints == null)
                    executionPoints = new List<ExecutionPoint>
                    {
                        new ExecutionPoint(BuildStep.GenerateIntermediateTableOfContents, ExecutionBehaviors.After)
                    };

                return executionPoints;
            }
        }

        /// <summary>
        /// This method is used by the Sandcastle Help File Builder to let the plug-in perform its own
        /// configuration.
        /// </summary>
        /// <param name="project">A reference to the active project</param>
        /// <param name="currentConfig">The current configuration XML fragment</param>
        /// <returns>A string containing the new configuration XML fragment</returns>
        /// <remarks>The configuration data will be stored in the help file builder project</remarks>
        public string ConfigurePlugIn(SandcastleProject project, string currentConfig)
        {
            using(HierarchicalTocConfigDlg dlg = new HierarchicalTocConfigDlg(currentConfig))
            {
                if(dlg.ShowDialog() == DialogResult.OK)
                    currentConfig = dlg.Configuration;
            }

            return currentConfig;
        }

        /// <summary>
        /// This method is used to initialize the plug-in at the start of the build process
        /// </summary>
        /// <param name="buildProcess">A reference to the current build process</param>
        /// <param name="configuration">The configuration data that the plug-in should use to initialize itself</param>
        public void Initialize(BuildProcess buildProcess, XPathNavigator configuration)
        {
            XPathNavigator root;
            string option;
            builder = buildProcess;
            minParts = 2;

            var metadata = (HelpFileBuilderPlugInExportAttribute)this.GetType().GetCustomAttributes(
                typeof(HelpFileBuilderPlugInExportAttribute), false).First();

            builder.ReportProgress("{0} Version {1}\r\n{2}", metadata.Id, metadata.Version, metadata.Copyright);

            // This plug-in cannot be used if the namespace grouping option is enabled as that replaces this
            // plug-in.
            if(builder.CurrentProject.NamespaceGrouping)
            {
                if(executionPoints == null)
                    executionPoints = new List<ExecutionPoint>();
                else
                    executionPoints.Clear();

                builder.ReportWarning("HTP0001", "The project being built has namespace grouping enabled which " +
                    "supersedes the Hierarchical TOC Plug-In.  The plug-in will not be used in this build.");
                return;
            }

            // The Hierarchical TOC plug-in is not compatible with MS Help Viewer output.  The problem is that
            // the table of contents is generated off of the help topics when the help viewer file is installed
            // and, since there are no physical topics for the namespace nodes added to the intermediate TOC file
            // by the plug-in, they do not appear in the help file.
            //
            // The project namespace grouping options supersede this plug-in and fix the above issue as topics
            // are generated for the namespace group entries.  As such, this plug-in is deprecated and will not
            // receive further changes.  It will available for the time being to support the older presentation
            // styles that do not support MS Help Viewer output or the namespace grouping project options.
            if((builder.CurrentProject.HelpFileFormat & HelpFileFormats.MSHelpViewer) != 0)
            {
                if(executionPoints == null)
                    executionPoints = new List<ExecutionPoint>();
                else
                    executionPoints.Clear();

                builder.ReportWarning("HTP0002", "This build produces MS Help Viewer output with which the " +
                    "Hierarchical TOC Plug-In is not compatible.  It will not be used in this build.");
                return;
            }

            // Load the configuration
            root = configuration.SelectSingleNode("configuration/toc");

            if(root != null)
            {
                option = root.GetAttribute("minParts", String.Empty);

                if(!String.IsNullOrEmpty(option))
                    minParts = Convert.ToInt32(option, CultureInfo.InvariantCulture);

                if(minParts < 1)
                    minParts = 1;

                option = root.GetAttribute("insertBelow", String.Empty);

                if(!String.IsNullOrEmpty(option))
                    insertBelow = Convert.ToBoolean(option, CultureInfo.InvariantCulture);
            }
        }

        /// <summary>
        /// This method is used to execute the plug-in during the build process
        /// </summary>
        /// <param name="context">The current execution context</param>
        public void Execute(ExecutionContext context)
        {
            List<string> namespaceList = new List<string>();
            Dictionary<string, XmlNode> namespaceNodes = new Dictionary<string, XmlNode>();
            XmlDocument toc;
            XPathNavigator root, navToc;
            XmlAttribute attr;
            XmlNode tocEntry = null, tocParent;
            string[] parts;
            string name, parent, topicTitle, referenceContentFile;
            int parentIdx, childIdx, entriesAdded;

            builder.ReportProgress("Retrieving namespace topic title from shared content...");

            referenceContentFile = Directory.EnumerateFiles(builder.PresentationStyleResourceItemsFolder,
                "reference*content*", SearchOption.AllDirectories).FirstOrDefault();

            if(referenceContentFile != null)
            {
                toc = new XmlDocument();
                toc.Load(referenceContentFile);
                tocEntry = toc.SelectSingleNode("content/item[@id='namespaceTopicTitle']");
            }

            if(tocEntry != null)
                topicTitle = tocEntry.InnerText;
            else
            {
                builder.ReportWarning("HTP0003", "Unable to locate namespace topic title in reference " +
                    "content file.  Using default.");
                topicTitle = "{0} Namespace";
            }

            builder.ReportProgress("Creating namespace hierarchy...");

            toc = new XmlDocument();
            toc.Load(builder.WorkingFolder + "toc.xml");
            navToc = toc.CreateNavigator();

            // Get a list of the namespaces.  If a root namespace container node is present, we need to look in
            // it rather than the document root node.
            root = navToc.SelectSingleNode("topics/topic[starts-with(@id, 'R:')]");

            if(root == null)
                root = navToc.SelectSingleNode("topics");

            foreach(XPathNavigator ns in root.Select("topic[starts-with(@id, 'N:')]"))
            {
                name = ns.GetAttribute("id", String.Empty);
                namespaceList.Add(name);
                namespaceNodes.Add(name, ((IHasXmlNode)ns).GetNode());
            }

            // See if any container nodes need to be created for namespaces with a common root name
            for(parentIdx = 0; parentIdx < namespaceList.Count; parentIdx++)
            {
                parts = namespaceList[parentIdx].Split('.');

                // Only do it for namespaces with a minimum number of parts
                if(parts.Length > minParts)
                {
                    for(childIdx = minParts; childIdx < parts.Length; childIdx++)
                    {
                        name = String.Join(".", parts, 0, childIdx);

                        if(!namespaceList.Contains(name))
                        {
                            if(namespaceList.FindAll(
                              ns => ns.StartsWith(name + ".", StringComparison.Ordinal)).Count > 0)
                            {
                                // The nodes will be created later once we know where to insert them
                                namespaceList.Add(name);
                                namespaceNodes.Add(name, null);
                            }
                        }
                    }
                }
            }

            // Sort them in reverse order
            namespaceList.Sort((n1, n2) => String.Compare(n2, n1, StringComparison.Ordinal));

            // If any container namespaces were added, create nodes for them and insert them before the namespace
            // ahead of them in the list.
            foreach(string key in namespaceList)
                if(namespaceNodes[key] == null)
                {
                    tocEntry = toc.CreateElement("topic");
                    attr = toc.CreateAttribute("id");

                    attr.Value = String.Format(CultureInfo.InvariantCulture, topicTitle,
                        (key.Length > 2) ? key.Substring(2) : "Global");
                    tocEntry.Attributes.Append(attr);

                    parentIdx = namespaceList.IndexOf(key);
                    tocParent = namespaceNodes[namespaceList[parentIdx - 1]];
                    tocParent.ParentNode.InsertBefore(tocEntry, tocParent);
                    namespaceNodes[key] = tocEntry;
                }

            for(parentIdx = 1; parentIdx < namespaceList.Count; parentIdx++)
            {
                parent = namespaceList[parentIdx];
                entriesAdded = 0;

                // Check each preceding namespace.  If it starts with the parent's name, insert it as a child of
                // that one.
                for(childIdx = 0; childIdx < parentIdx; childIdx++)
                {
                    name = namespaceList[childIdx];
                    
                    if(name.StartsWith(parent + ".", StringComparison.Ordinal))
                    {
                        tocEntry = namespaceNodes[name];
                        tocParent = namespaceNodes[parent];

                        if(insertBelow && entriesAdded < tocParent.ChildNodes.Count)
                            tocParent.InsertAfter(tocEntry, tocParent.ChildNodes[
                                tocParent.ChildNodes.Count - entriesAdded - 1]);
                        else
                            tocParent.InsertBefore(tocEntry, tocParent.ChildNodes[0]);

                        namespaceList.RemoveAt(childIdx);
                        entriesAdded++;
                        parentIdx--;
                        childIdx--;
                    }
                }
            }

            toc.Save(builder.WorkingFolder + "toc.xml");
        }
        #endregion

        #region IDisposable implementation
        //=====================================================================

        /// <summary>
        /// This implements the Dispose() interface to properly dispose of the plug-in object
        /// </summary>
        public void Dispose()
        {
            // Nothing to dispose of in this one
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
