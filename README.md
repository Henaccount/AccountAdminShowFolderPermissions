# AccountAdminShowFolderPermissions
Proof of Concept (code example, use at own risk, no support): as an BIM 360 Account Admin, show folder permissions for users, companies or roles

Installation: 

First download and install this github code example: https://github.com/Autodesk-Forge/forge-viewhubs/tree/eb7cb963e75ac371b443e2850ef1e228b7506dfb
Now replace the files from this repositry where you find them in the original example..

How to use (see formatted version in the tool):

              Options:<br /><br />
              <b>userId:</b> you get this from the account admin user section, just grab the Id from the URL after selection a certain user: https://admin.b360.autodesk.com/admin/ae8c2a83-81e1-438a-8a59-4540e84779c0/users/<b>483069d9-f87d-40a0-9230-bc35c6870423</b> All folder permissions connected to this user will be displayed (by email, by company, by roles)<br />
              <b>by_emails_only, companies, roles:</b> by_emails_only means explicit permissions given by email adress. You can type several email, companies or roles seperated by comma. Like this you can compare permissions given by email, company or role.<br /><br />
              <br />
              Usage:<br /><br />
              e.g. copy/paste userId into the text field, then click the refresh button next to the text field. Now you can expand the tree by the small triangles in front of the tree folders. Permissions will shown when you expand. When you want to open the whole subtree under a particular folder, click on the folder's text. If it doesn't open the whole tree, click again on this folder, so it will open the remaining folders.
              <br /><br /><br />
              Permissions:<br /><br />
              In the following X replaces "1" or "2".<br />
              "1": permissions set on this folder level<br />
              "2": permissions inherited from the parent folder<br /><br />
              X0000: View only<br />
              XX000: View and Download<br />
              00X00: Upload only<br />
              XXX00: View and Download and Upload<br />
              XXXX0: View and Download and Upload and Edit<br />
              XXXXX: Folder Control<br /><br /><br />
              (Why permission query takes so long? <a href="https://forge.autodesk.com/en/docs/oauth/v2/developers_guide/rate-limiting/forge-rate-limits/">see here</a>)
 
