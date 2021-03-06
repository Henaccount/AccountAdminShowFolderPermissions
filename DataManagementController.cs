/////////////////////////////////////////////////////////////////////
// Copyright (c) Autodesk, Inc. All rights reserved
// Written by Forge Partner Development
//
// Permission to use, copy, modify, and distribute this software in
// object code form for any purpose and without fee is hereby granted,
// provided that the above copyright notice appears in all copies and
// that both that copyright notice and the limited warranty and
// restricted rights notice below appear in all supporting
// documentation.
//
// AUTODESK PROVIDES THIS PROGRAM "AS IS" AND WITH ALL FAULTS.
// AUTODESK SPECIFICALLY DISCLAIMS ANY IMPLIED WARRANTY OF
// MERCHANTABILITY OR FITNESS FOR A PARTICULAR USE.  AUTODESK, INC.
// DOES NOT WARRANT THAT THE OPERATION OF THE PROGRAM WILL BE
// UNINTERRUPTED OR ERROR FREE.
/////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Autodesk.Forge;
using Autodesk.Forge.Model;
using Newtonsoft.Json.Linq;
using System.Net;
using Microsoft.AspNetCore.Http;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using System.Diagnostics;
using RestSharp;
using System.Linq;
using System.Text;

namespace forgeSample.Controllers
{
    public class DataManagementController : ControllerBase
    {
        /*mb private IHostingEnvironment _env;
        public DataManagementController(IHostingEnvironment env)
        {
            _env = env;
        } mb*/
        public DataManagementController()
        {

        }
        /// <summary>
        /// Credentials on this request
        /// </summary>
        private Credentials Credentials { get; set; }

        /// <summary>
        /// GET TreeNode passing the ID
        /// </summary>
        [HttpGet]
        [Route("api/forge/datamanagement")]
        public async Task<IList<jsTreeNode>> GetTreeNodeAsync(string id, string qtype, string qtext)
        {
            Credentials = await Credentials.FromSessionAsync(base.Request.Cookies, Response.Cookies);
            if (Credentials == null) { return null; }

            IList<jsTreeNode> nodes = new List<jsTreeNode>();

            if (id == "#") // root
                return await GetHubsAsync();
            else
            {
                string[] idParams = id.Split('/');
                string resource = idParams[idParams.Length - 2];
                switch (resource)
                {
                    case "hubs": // hubs node selected/expanded, show projects
                        return await GetProjectsAsync(id);
                    case "projects": // projects node selected/expanded, show root folder contents
                        return await GetProjectContents(id);
                    case "folders": // folders node selected/expanded, show folder contents
                        return await GetFolderContents(id, qtype, qtext);
                    case "items":
                        return await GetItemVersions(id);
                }
            }

            return nodes;
        }

        private async Task<IList<jsTreeNode>> GetHubsAsync()
        {
            IList<jsTreeNode> nodes = new List<jsTreeNode>();

            // the API SDK
            HubsApi hubsApi = new HubsApi();
            hubsApi.Configuration.AccessToken = Credentials.TokenInternal;

            var hubs = await hubsApi.GetHubsAsync();
            foreach (KeyValuePair<string, dynamic> hubInfo in new DynamicDictionaryItems(hubs.data))
            {
                // check the type of the hub to show an icon
                string nodeType = "hubs";
                switch ((string)hubInfo.Value.attributes.extension.type)
                {
                    case "hubs:autodesk.core:Hub":
                        nodeType = "hubs"; // if showing only BIM 360, mark this as 'unsupported'
                        break;
                    case "hubs:autodesk.a360:PersonalHub":
                        nodeType = "personalHub"; // if showing only BIM 360, mark this as 'unsupported'
                        break;
                    case "hubs:autodesk.bim360:Account":
                        nodeType = "bim360Hubs";
                        break;
                }

                //mb: only bim 360
                if (!nodeType.Equals("bim360Hubs")) continue;

                // create a treenode with the values
                jsTreeNode hubNode = new jsTreeNode(hubInfo.Value.links.self.href, hubInfo.Value.attributes.name, nodeType, !(nodeType == "unsupported"));
                nodes.Add(hubNode);
            }

            return nodes;
        }

        private async Task<IList<jsTreeNode>> GetProjectsAsync(string href)
        {
            IList<jsTreeNode> nodes = new List<jsTreeNode>();

            // the API SDK
            ProjectsApi projectsApi = new ProjectsApi();
            projectsApi.Configuration.AccessToken = Credentials.TokenInternal;

            // extract the hubId from the href
            string[] idParams = href.Split('/');
            string hubId = idParams[idParams.Length - 1];

            var projects = await projectsApi.GetHubProjectsAsync(hubId);
            foreach (KeyValuePair<string, dynamic> projectInfo in new DynamicDictionaryItems(projects.data))
            {
                // check the type of the project to show an icon
                string nodeType = "projects";
                switch ((string)projectInfo.Value.attributes.extension.type)
                {
                    case "projects:autodesk.core:Project":
                        nodeType = "a360projects";
                        break;
                    case "projects:autodesk.bim360:Project":
                        nodeType = "bim360projects";
                        break;
                }

                // create a treenode with the values
                jsTreeNode projectNode = new jsTreeNode(projectInfo.Value.links.self.href, projectInfo.Value.attributes.name, nodeType, true);
                nodes.Add(projectNode);
            }

            return nodes;
        }

        private async Task<IList<jsTreeNode>> GetProjectContents(string href)
        {
            IList<jsTreeNode> nodes = new List<jsTreeNode>();

            // the API SDK
            ProjectsApi projectApi = new ProjectsApi();
            projectApi.Configuration.AccessToken = Credentials.TokenInternal;

            // extract the hubId & projectId from the href
            string[] idParams = href.Split('/');
            string hubId = idParams[idParams.Length - 3];
            string projectId = idParams[idParams.Length - 1];

            /*var project = await projectApi.GetProjectAsync(hubId, projectId);
            var rootFolderHref = project.data.relationships.rootFolder.meta.link.href;
            return await GetFolderContents(rootFolderHref);*/

            var folders = await projectApi.GetProjectTopFoldersAsync(hubId, projectId);
            foreach (KeyValuePair<string, dynamic> folder in new DynamicDictionaryItems(folders.data))
            {
                nodes.Add(new jsTreeNode(folder.Value.links.self.href, folder.Value.attributes.displayName, "folders", true));
            }
            return nodes;
        }

        private async Task<IList<jsTreeNode>> GetFolderContents(string href, string qtype, string qtext)
        {
            IList<jsTreeNode> nodes = new List<jsTreeNode>();

            // the API SDK
            FoldersApi folderApi = new FoldersApi();
            folderApi.Configuration.AccessToken = Credentials.TokenInternal;

            // extract the projectId & folderId from the href
            string[] idParams = href.Split('/');
            string folderId = idParams[idParams.Length - 1];
            string projectId = idParams[idParams.Length - 3];

            // check if folder specifies visible types
            JArray visibleTypes = null;
            dynamic folder = (await folderApi.GetFolderAsync(projectId, folderId)).ToJson();
            if (folder.data.attributes != null && folder.data.attributes.extension != null && folder.data.attributes.extension.data != null && !(folder.data.attributes.extension.data is JArray) && folder.data.attributes.extension.data.visibleTypes != null)
                visibleTypes = folder.data.attributes.extension.data.visibleTypes;

            var folderContents = await folderApi.GetFolderContentsAsync(projectId, folderId);
            // the GET Folder Contents has 2 main properties: data & included (not always available)
            var folderData = new DynamicDictionaryItems(folderContents.data);
            var folderIncluded = (folderContents.Dictionary.ContainsKey("included") ? new DynamicDictionaryItems(folderContents.included) : null);

            // let's start iterating the FOLDER DATA
            foreach (KeyValuePair<string, dynamic> folderContentItem in folderData)
            {
                // do we need to skip some items? based on the visibleTypes of this folder
                string extension = folderContentItem.Value.attributes.extension.type;
                if (extension.IndexOf("Folder") /*any folder*/ == -1 && visibleTypes != null && !visibleTypes.ToString().Contains(extension)) continue;

                // if the type is items:autodesk.bim360:Document we need some manipulation...
                if (extension.Equals("items:autodesk.bim360:Document"))
                {
                    //mb
                    continue;
                    // as this is a DOCUMENT, lets interate the FOLDER INCLUDED to get the name (known issue)
                    foreach (KeyValuePair<string, dynamic> includedItem in folderIncluded)
                    {
                        // check if the id match...
                        if (includedItem.Value.relationships.item.data.id.IndexOf(folderContentItem.Value.id) != -1)
                        {
                            // found it! now we need to go back on the FOLDER DATA to get the respective FILE for this DOCUMENT
                            foreach (KeyValuePair<string, dynamic> folderContentItem1 in folderData)
                            {
                                if (folderContentItem1.Value.attributes.extension.type.IndexOf("File") == -1) continue; // skip if type is NOT File

                                // check if the sourceFileName match...
                                if (folderContentItem1.Value.attributes.extension.data.sourceFileName == includedItem.Value.attributes.extension.data.sourceFileName)
                                {
                                    // ready!

                                    // let's return for the jsTree with a special id:
                                    // itemUrn|versionUrn|viewableId
                                    // itemUrn: used as target_urn to get document issues
                                    // versionUrn: used to launch the Viewer
                                    // viewableId: which viewable should be loaded on the Viewer
                                    // this information will be extracted when the user click on the tree node, see ForgeTree.js:136 (activate_node.jstree event handler)
                                    string treeId = string.Format("{0}|{1}|{2}",
                                        folderContentItem.Value.id, // item urn
                                        Base64Encode(folderContentItem1.Value.relationships.tip.data.id), // version urn
                                        includedItem.Value.attributes.extension.data.viewableId // viewableID
                                    );
                                    nodes.Add(new jsTreeNode(treeId, WebUtility.UrlDecode(includedItem.Value.attributes.name), "bim360documents", false));
                                }
                            }
                        }
                    }
                }
                else if (extension.Equals("folders:autodesk.bim360:Folder"))
                {
                    //mb: , string qtype, string qtext
                    string legend = "";
                    string permissions = "";
                    string userId = "";
                    string[] useremails = null;
                    string[] companies = null;
                    string[] roles = null;

                    if (qtype.Equals("userId"))
                    {
                        //get user company and roles
                        userId = qtext.Trim();
                        string utoken = Credentials.TokenInternal;
                        var uclient = new RestClient("https://developer.api.autodesk.com/bim360/admin/v1/projects/" + projectId.Substring(2) + "/users/" + userId);
                        uclient.Timeout = -1;
                        var urequest = new RestRequest(Method.GET);
                        urequest.AddHeader("Content-Type", "application/json");
                        urequest.AddHeader("Accept", "application/json");
                        urequest.AddHeader("Authorization", "Bearer " + utoken);
                        IRestResponse uresponse = uclient.Execute(urequest);
                        JObject ures = JObject.Parse(uresponse.Content);
                        useremails = new string[] { ures["email"].ToString() };
                        companies = new string[] { ures["companyId"].ToString() };
                        List<string> lroles = new List<string>();
                        foreach (var item in ures["roleIds"].Children())
                        {
                            lroles.Add(item.ToString());
                        }
                        roles = lroles.ToArray();
                    }
                    else if (qtype.Equals("by_emails_only"))
                    {
                        useremails = qtext.Split(new Char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    }
                    else if (qtype.Equals("companies"))
                    {
                        companies = qtext.Split(new Char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    }
                    else if (qtype.Equals("roles"))
                    {
                        roles = qtext.Split(new Char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    }

                    string token = Credentials.TokenInternal;
                    string folderUrn = folderContentItem.Value.links.self.href.ToString();
                    folderUrn = folderUrn.Split("/folders/")[1].Split("/contents?")[0].Replace(":", "%3A");
                    var client = new RestClient("https://developer.api.autodesk.com/bim360/docs/v1/projects/" + projectId.Substring(2) + "/folders/" + folderUrn + "/permissions");
                    client.Timeout = -1;
                    var request = new RestRequest(Method.GET);
                    request.AddHeader("Content-Type", "application/vnd.api+json");
                    request.AddHeader("Accept", "application/vnd.api+json");
                    request.AddHeader("Authorization", "Bearer " + token);

                    try
                    {
                        bool validresponse = false;
                        int countvr = 0;
                        int maxvr = 3;
                        JArray res = null;
                        while (!validresponse)
                        {
                            ++countvr;
                            IRestResponse response = client.Execute(request);
                            //Console.WriteLine(response.Content);
                            try
                            {
                                res = JArray.Parse(response.Content);
                                validresponse = true;
                            }
                            catch
                            {
                                if (countvr >= maxvr) break;
                                System.Threading.Thread.Sleep(25000);
                            }
                        }

                        

                        foreach (var item in res.Children())
                        {
                            dynamic data = JObject.Parse(item.ToString());
                            if (data["subjectType"].ToString().Equals("USER") && useremails != null)
                            {
                                int count = 0;
                                foreach (string useremail in useremails)
                                {
                                    ++count;
                                    if (data["email"].ToString().Equals(useremail.Trim()))
                                    {
                                        permissions += " " + GetPermissionString(data, count, useremail);
                                    }
                                }
                            }
                            else if (data["subjectType"].ToString().Equals("COMPANY") && companies != null)
                            {
                                int count = 3;
                                foreach (string company in companies) 
                                {
                                    ++count;
                                    if (data["name"].ToString().Equals(company.Trim()) || data["subjectId"].ToString().Equals(company.Trim()))
                                    {
                                        permissions += " " + GetPermissionString(data, count, "C:" + data["name"].ToString());
                                    }
                                }
                            }
                            else if (data["subjectType"].ToString().Equals("ROLE") && roles != null)
                            {
                                int count = 6;
                                foreach (string role in roles)
                                {
                                    ++count;
                                    if (data["name"].ToString().Equals(role.Trim()) || data["subjectId"].ToString().Equals(role.Trim()))
                                    {
                                        permissions += " " + GetPermissionString(data, count, "R:" + data["name"].ToString());
                                    }
                                }
                            }

                        }

                    }
                    catch (Exception e) { permissions = e.Message + "###" + e.StackTrace; }


                    // non-Plans folder items
                    //if (folderContentItem.Value.attributes.hidden == true) continue;
                    nodes.Add(new jsTreeNode(folderContentItem.Value.links.self.href, folderContentItem.Value.attributes.displayName + permissions, (string)folderContentItem.Value.type, true));

                }
            }
            //nodes.Add(new jsTreeNode("", "long long text", "folders", false));
            return nodes;
        }

        public static string GetPermissionString(dynamic data, int num, string owner)
        {
            /*from: https://stackoverflow.com/questions/16999604/convert-string-to-hex-string-in-c-sharp
            byte[] ba = Encoding.Default.GetBytes(owner);
            var hexString = BitConverter.ToString(ba);
            hexString = hexString.Replace("-", "").Substring(0,6);*/
            
            string permissions = "";
            permissions += "<span class=\"s" + num + "\">" + owner + " : ";
            int sumactions = 0;
            int actions = 0, inheritactions = 0;

            actions = GetCodedPermissions(data["actions"].ToString());

            inheritactions = GetCodedPermissions(data["inheritActions"].ToString()) * 2;

            sumactions = actions + inheritactions;
            string displayedpermissions = sumactions.ToString().PadLeft(5, '0').Replace("3", "2");
            permissions += displayedpermissions;
            permissions += "</span>";
            return permissions;
        }

        public static int GetCodedPermissions(string a)
        {
            int codedpermissions = 0;

            if (a.Contains("VIEW") && a.Contains("COLLABORATE")) codedpermissions += 10000;
            if (a.Contains("DOWNLOAD")) codedpermissions += 1000;
            if (a.Contains("PUBLISH")) codedpermissions += 100;
            if (a.Contains("EDIT")) codedpermissions += 10;
            if (a.Contains("CONTROL")) codedpermissions += 1;

            return codedpermissions;
        }

        private string GetName(DynamicDictionaryItems folderIncluded, KeyValuePair<string, dynamic> folderContentItem)
        {


            return "N/A";
        }

        private async Task<IList<jsTreeNode>> GetItemVersions(string href)
        {
            IList<jsTreeNode> nodes = new List<jsTreeNode>();

            // the API SDK
            ItemsApi itemApi = new ItemsApi();
            itemApi.Configuration.AccessToken = Credentials.TokenInternal;

            // extract the projectId & itemId from the href
            string[] idParams = href.Split('/');
            string itemId = idParams[idParams.Length - 1];
            string projectId = idParams[idParams.Length - 3];

            var versions = await itemApi.GetItemVersionsAsync(projectId, itemId);
            foreach (KeyValuePair<string, dynamic> version in new DynamicDictionaryItems(versions.data))
            {
                DateTime versionDate = version.Value.attributes.lastModifiedTime;
                string verNum = version.Value.id.Split("=")[1];
                string userName = version.Value.attributes.lastModifiedUserName;

                string urn = string.Empty;
                try { urn = (string)version.Value.relationships.derivatives.data.id; }
                catch { urn = Base64Encode(version.Value.id); } // some BIM 360 versions don't have viewable

                jsTreeNode node = new jsTreeNode(
                    urn,
                    string.Format("v{0}: {1} by {2}", verNum, versionDate.ToString("dd/MM/yy HH:mm:ss"), userName),
                    "versions",
                    false);
                nodes.Add(node);
            }

            return nodes;
        }

        public static string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes).Replace("/", "_");
        }

        public class jsTreeNode
        {
            public jsTreeNode(string id, string text, string type, bool children)
            {
                this.id = id;
                this.text = text;
                this.type = type;
                this.children = children;
            }

            public string id { get; set; }
            public string text { get; set; }
            public string type { get; set; }
            public bool children { get; set; }
        }

        private const int UPLOAD_CHUNK_SIZE = 5; // Mb

        /// <summary>
        /// Receive a file from the client and upload to the bucket
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [Route("api/forge/datamanagement")]
        //public async Task<dynamic> UploadObject([FromForm]UploadFile input)
        public async Task<dynamic> UploadObject(Stream input)//mb
        {

            // get the uploaded file and save on the server
            /*mb var fileSavePath = Path.Combine(_env.ContentRootPath, input.fileToUpload.FileName);
            using (var stream = new FileStream(fileSavePath, FileMode.Create))
                await input.fileToUpload.CopyToAsync(stream); mb*/

            // user credentials
            Credentials = await Credentials.FromSessionAsync(base.Request.Cookies, Response.Cookies);

            // extract projectId and folderId from folderHref
            /*mb string[] hrefParams = input.folderHref.Split("/");
            string projectId = hrefParams[hrefParams.Length - 3];
            string folderId = hrefParams[hrefParams.Length - 1]; mb*/
            string projectId = "3be23c1c-1383-440a-b395-ac5933f797a1";
            string folderId = "urn:adsk.wipprod:fs.folder:co.-w2D5-voTQiB-6-aowONzg";
            string fileName = "result.ifc";

            // prepare storage
            ProjectsApi projectApi = new ProjectsApi();
            projectApi.Configuration.AccessToken = Credentials.TokenInternal;
            StorageRelationshipsTargetData storageRelData = new StorageRelationshipsTargetData(StorageRelationshipsTargetData.TypeEnum.Folders, folderId);
            CreateStorageDataRelationshipsTarget storageTarget = new CreateStorageDataRelationshipsTarget(storageRelData);
            CreateStorageDataRelationships storageRel = new CreateStorageDataRelationships(storageTarget);
            BaseAttributesExtensionObject attributes = new BaseAttributesExtensionObject(string.Empty, string.Empty, new JsonApiLink(string.Empty), null);
            //mb CreateStorageDataAttributes storageAtt = new CreateStorageDataAttributes(input.fileToUpload.FileName, attributes);
            CreateStorageDataAttributes storageAtt = new CreateStorageDataAttributes(fileName, attributes);
            CreateStorageData storageData = new CreateStorageData(CreateStorageData.TypeEnum.Objects, storageAtt, storageRel);
            CreateStorage storage = new CreateStorage(new JsonApiVersionJsonapi(JsonApiVersionJsonapi.VersionEnum._0), storageData);
            dynamic storageCreated = await projectApi.PostStorageAsync(projectId, storage);

            string[] storageIdParams = ((string)storageCreated.data.id).Split('/');
            string[] bucketKeyParams = storageIdParams[storageIdParams.Length - 2].Split(':');
            string bucketKey = bucketKeyParams[bucketKeyParams.Length - 1];
            string objectName = storageIdParams[storageIdParams.Length - 1];

            // upload the file/object, which will create a new object
            ObjectsApi objects = new ObjectsApi();
            objects.Configuration.AccessToken = Credentials.TokenInternal;

            // get file size
            //mb long fileSize = (new FileInfo(fileSavePath)).Length;
            long fileSize = 10;

            // decide if upload direct or resumable (by chunks)
            if (fileSize > UPLOAD_CHUNK_SIZE * 1024 * 1024) // upload in chunks
            {
                /*mb long chunkSize = 2 * 1024 * 1024; // 2 Mb
                long numberOfChunks = (long)Math.Round((double)(fileSize / chunkSize)) + 1;

                long start = 0;
                chunkSize = (numberOfChunks > 1 ? chunkSize : fileSize);
                long end = chunkSize;
                string sessionId = Guid.NewGuid().ToString();

                // upload one chunk at a time
                using (BinaryReader reader = new BinaryReader(new FileStream(fileSavePath, FileMode.Open)))
                {
                    for (int chunkIndex = 0; chunkIndex < numberOfChunks; chunkIndex++)
                    {
                        string range = string.Format("bytes {0}-{1}/{2}", start, end, fileSize);

                        long numberOfBytes = chunkSize + 1;
                        byte[] fileBytes = new byte[numberOfBytes];
                        MemoryStream memoryStream = new MemoryStream(fileBytes);
                        reader.BaseStream.Seek((int)start, SeekOrigin.Begin);
                        int count = reader.Read(fileBytes, 0, (int)numberOfBytes);
                        memoryStream.Write(fileBytes, 0, (int)numberOfBytes);
                        memoryStream.Position = 0;

                        await objects.UploadChunkAsync(bucketKey, objectName, (int)numberOfBytes, range, sessionId, memoryStream);

                        start = end + 1;
                        chunkSize = ((start + chunkSize > fileSize) ? fileSize - start - 1 : chunkSize);
                        end = start + chunkSize;
                    }
                } mb*/
            }
            else // upload in a single call
            {
                /*mb using (StreamReader streamReader = new StreamReader(fileSavePath))
                 {
                     await objects.UploadObjectAsync(bucketKey, objectName, (int)streamReader.BaseStream.Length, streamReader.BaseStream, "application/octet-stream");
                 } mb*/
                await objects.UploadObjectAsync(bucketKey, objectName, (int)input.Length, input, "application/octet-stream");
            }

            // cleanup
            /*mb string fileName = input.fileToUpload.FileName;
            System.IO.File.Delete(fileSavePath); mb*/

            // check if file already exists...
            FoldersApi folderApi = new FoldersApi();
            folderApi.Configuration.AccessToken = Credentials.TokenInternal;
            var filesInFolder = await folderApi.GetFolderContentsAsync(projectId, folderId);
            string itemId = string.Empty;
            foreach (KeyValuePair<string, dynamic> item in new DynamicDictionaryItems(filesInFolder.data))
                if (item.Value.attributes.displayName == fileName)
                    itemId = item.Value.id; // this means a file with same name is already there, so we'll create a new version

            // now decide whether create a new item or new version
            if (string.IsNullOrWhiteSpace(itemId))
            {
                // create a new item
                BaseAttributesExtensionObject baseAttribute = new BaseAttributesExtensionObject(projectId.StartsWith("a.") ? "items:autodesk.core:File" : "items:autodesk.bim360:File", "1.0");
                CreateItemDataAttributes createItemAttributes = new CreateItemDataAttributes(fileName, baseAttribute);
                CreateItemDataRelationshipsTipData createItemRelationshipsTipData = new CreateItemDataRelationshipsTipData(CreateItemDataRelationshipsTipData.TypeEnum.Versions, CreateItemDataRelationshipsTipData.IdEnum._1);
                CreateItemDataRelationshipsTip createItemRelationshipsTip = new CreateItemDataRelationshipsTip(createItemRelationshipsTipData);
                StorageRelationshipsTargetData storageTargetData = new StorageRelationshipsTargetData(StorageRelationshipsTargetData.TypeEnum.Folders, folderId);
                CreateStorageDataRelationshipsTarget createStorageRelationshipTarget = new CreateStorageDataRelationshipsTarget(storageTargetData);
                CreateItemDataRelationships createItemDataRelationhips = new CreateItemDataRelationships(createItemRelationshipsTip, createStorageRelationshipTarget);
                CreateItemData createItemData = new CreateItemData(CreateItemData.TypeEnum.Items, createItemAttributes, createItemDataRelationhips);
                BaseAttributesExtensionObject baseAttExtensionObj = new BaseAttributesExtensionObject(projectId.StartsWith("a.") ? "versions:autodesk.core:File" : "versions:autodesk.bim360:File", "1.0");
                CreateStorageDataAttributes storageDataAtt = new CreateStorageDataAttributes(fileName, baseAttExtensionObj);
                CreateItemRelationshipsStorageData createItemRelationshipsStorageData = new CreateItemRelationshipsStorageData(CreateItemRelationshipsStorageData.TypeEnum.Objects, storageCreated.data.id);
                CreateItemRelationshipsStorage createItemRelationshipsStorage = new CreateItemRelationshipsStorage(createItemRelationshipsStorageData);
                CreateItemRelationships createItemRelationship = new CreateItemRelationships(createItemRelationshipsStorage);
                CreateItemIncluded includedVersion = new CreateItemIncluded(CreateItemIncluded.TypeEnum.Versions, CreateItemIncluded.IdEnum._1, storageDataAtt, createItemRelationship);
                CreateItem createItem = new CreateItem(new JsonApiVersionJsonapi(JsonApiVersionJsonapi.VersionEnum._0), createItemData, new List<CreateItemIncluded>() { includedVersion });

                ItemsApi itemsApi = new ItemsApi();
                itemsApi.Configuration.AccessToken = Credentials.TokenInternal;
                var newItem = await itemsApi.PostItemAsync(projectId, createItem);
                return newItem;
            }
            else
            {
                // create a new version
                BaseAttributesExtensionObject attExtensionObj = new BaseAttributesExtensionObject(projectId.StartsWith("a.") ? "versions:autodesk.core:File" : "versions:autodesk.bim360:File", "1.0");
                CreateStorageDataAttributes storageDataAtt = new CreateStorageDataAttributes(fileName, attExtensionObj);
                CreateVersionDataRelationshipsItemData dataRelationshipsItemData = new CreateVersionDataRelationshipsItemData(CreateVersionDataRelationshipsItemData.TypeEnum.Items, itemId);
                CreateVersionDataRelationshipsItem dataRelationshipsItem = new CreateVersionDataRelationshipsItem(dataRelationshipsItemData);
                CreateItemRelationshipsStorageData itemRelationshipsStorageData = new CreateItemRelationshipsStorageData(CreateItemRelationshipsStorageData.TypeEnum.Objects, storageCreated.data.id);
                CreateItemRelationshipsStorage itemRelationshipsStorage = new CreateItemRelationshipsStorage(itemRelationshipsStorageData);
                CreateVersionDataRelationships dataRelationships = new CreateVersionDataRelationships(dataRelationshipsItem, itemRelationshipsStorage);
                CreateVersionData versionData = new CreateVersionData(CreateVersionData.TypeEnum.Versions, storageDataAtt, dataRelationships);
                CreateVersion newVersionData = new CreateVersion(new JsonApiVersionJsonapi(JsonApiVersionJsonapi.VersionEnum._0), versionData);

                VersionsApi versionsApis = new VersionsApi();
                versionsApis.Configuration.AccessToken = Credentials.TokenInternal;
                dynamic newVersion = await versionsApis.PostVersionAsync(projectId, newVersionData);
                return newVersion;
            }
        }

        public class UploadFile
        {
            //[ModelBinder(BinderType = typeof(FormDataJsonBinder))]
            public string folderHref { get; set; }
            public IFormFile fileToUpload { get; set; }
            // Other properties
        }

    }
}