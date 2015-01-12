﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using Sshfs.GuiBackend.Remoteable;
using System.Runtime.Remoting;
using System.ServiceModel;
using Sshfs.GuiBackend;


namespace GUI_WindowsForms
{
    public partial class FiSSHForm : Form
    {

        Boolean Expanded = true;
        Boolean gBox2Vis = false;
        int TimerCount = 0;
        Font font = new Font("Microsoft Sans Serif", (float) 8, FontStyle.Regular);

        
        //////////////////////////////////////////////
        // For connection with Backend

        // Server connection object
        IServiceFisshBone bone_server;// = IPCConnection.ServerConnect();
        List<ServerModel> datamodel;
        private System.Threading.Thread MountThread = null; // Thread for mounting
        private Queue<Tuple<Guid, Guid>> ToMount = new Queue<Tuple<Guid,Guid>>(); // Mailbox for the Thread
        private List<Tuple<Guid, Guid>> MountingIDs = new List<Tuple<Guid, Guid>>();



        public FiSSHForm()
        {
            // get server object, has been already connected in Main()
            bone_server = IPCConnection.bone_client;

            InitializeComponent();
        }

        private void UpdateTreeView(/* STUFF */)
        {
            // get all data from backend
            try { datamodel = bone_server.listAll(); }
            catch
            { 
                MessageBox.Show("Cannot connect with server."); 
                datamodel = new List<ServerModel>();
            }
            
            foreach (ServerModel i in datamodel)
            {
                // Adding server node 
                TreeNode ParentNode = treeView1.Nodes.Add(String.Format(
                                        "Name: " + i.Name + Environment.NewLine +
                                        "IP: " + i.Host + Environment.NewLine +
                                        "Notes: " + i.Notes));
                ParentNode.SelectedImageIndex = 6;
                ParentNode.ImageIndex = 6;
                ParentNode.ContextMenuStrip = this.contextMenuStrip1;
                i.gui_node = ParentNode;


                // Adding folder nodes
                foreach (FolderModel j in i.Folders)
                {
                    TreeNode ChildNode = ParentNode.Nodes.Add( String.Format(
                                        "Name: " + j.Name + Environment.NewLine + 
                                        "Path: " + j.Folder + Environment.NewLine + 
                                        "Note: " + j.Note));
                    j.gui_node = ChildNode;
                    ChildNode.SelectedImageIndex = 4;
                    ChildNode.ImageIndex = 4;
                    ChildNode.ContextMenuStrip = this.contextMenuStrip2;
                }

                // Adding "new folder" node
                TreeNode Node = ParentNode.Nodes.Add(String.Format(
                                        "\n" + Environment.NewLine + 
                                        "Add new Folder" + Environment.NewLine));
                Node.SelectedImageIndex = 3;
                Node.ImageIndex = 3;
            }

            // Adding "new server" node
            TreeNode Node2  = treeView1.Nodes.Add(String.Format(
                                        "\n" + Environment.NewLine + 
                                        "Add new Server" + Environment.NewLine));
            Node2.SelectedImageIndex = 5;
            Node2.ImageIndex = 5;
        }

        private void GetDataFromServer()
        {
            List<ServerModel> tmp = new List<ServerModel>(datamodel);
            
            try { datamodel = bone_server.listAll(); }
            catch
            {
                MessageBox.Show("Cannot connect with server.");
                return;
            }

            foreach(ServerModel i in datamodel)
            {
                ServerModel tmp_server = tmp.Find(x => x.ID == i.ID);
                i.gui_node = tmp_server.gui_node;

                foreach(FolderModel j in i.Folders)
                {
                    FolderModel tmp_folder = tmp_server.Folders.Find(x => x.ID == j.ID);
                    j.gui_node = tmp_folder.gui_node;
                }
            }
        }

        
        // Updates menu strip and edit area
        private void ServerFolderEdit()
        {
            // While mounting no data updates
            if (MountingIDs.Count() == 0)
            { GetDataFromServer(); }

            if (treeView1.SelectedNode == null) { return; }

            switch (treeView1.SelectedNode.Level)
            {
                case 0:
                    if (Expanded)
                    {
                        groupBox1.Enabled = true;
                        groupBox2.Visible = false;
                        
                        // get server which is presented by selected node 
                        ServerModel server = datamodel.Find(x => treeView1.SelectedNode.Equals(x.gui_node));

                        mountToolStripMenuItem.Enabled = false;
                        unmountToolStripMenuItem.Enabled = false;
                        MountAnimatonStop();


                        if (server != null)
                        {
                            // write data in edit box
                            textBox_server_name.Text = server.Name;
                            textBox_server_ip.Text = server.Host;
                            numericUpDown_server_port.Value = server.Port;
                            richTextBox_server_notes.Text = server.Notes;
                            textbox_server_username.Text = server.Username;
                            textBox_server_privatkey.Text = server.PrivateKey;
                            textBox_server_password.Text = server.Password;
                            
                            groupBox1.Enabled = true;
                            groupBox2.Enabled = false;
                            groupBox3.Enabled = false;
                        }
                        else
                        {
                            // write data in edit box
                            textBox_server_name.Text = null;
                            textBox_server_ip.Text = null;
                            numericUpDown_server_port.Value = 22;
                            richTextBox_server_notes.Text = null;
                            textbox_server_username.Text = null;
                            textBox_server_privatkey.Text = null;
                            textBox_server_password.Text = null;
                            
                            groupBox1.Enabled = false;
                            groupBox2.Enabled = false;
                            groupBox3.Enabled = false;
                        }
                    }
                    else gBox2Vis = false;
                    break;
                case 1:
                    if (Expanded)
                    {
                        groupBox1.Enabled = false;
                        groupBox2.Visible = true;

                        // get server which is presented by selected parent node
                        ServerModel server = datamodel.Find(x => treeView1.SelectedNode.Parent.Equals(x.gui_node));
                        // get folder which is presented by selected node
                        FolderModel folder = null;
                        try { folder = server.Folders.Find(x => treeView1.SelectedNode.Equals(x.gui_node)); }
                        catch { }

                        if (folder != null)
                        {
                            // write data in edit box
                            textBox_folder_entry.Text = folder.Name;
                            textBox_folder_password.Text = folder.Password;
                            textBox_folder_privat_key.Text = folder.PrivatKey;
                            textBox8_folder_username.Text = folder.Username;
                            textBox9_folder_remotedirectory.Text = folder.Folder;
                            checkBox_folder_usedefaultaccound.Checked = folder.use_global_login;
                            comboBox_folder_driveletter.SelectedIndex = comboBox_folder_driveletter.Items.IndexOf(folder.Letter + ":");

                           
                            switch (folder.Status)
                            {
                                case Sshfs.DriveStatus.Mounted:
                                    MountAnimatonStop();
                                    mountToolStripMenuItem.Enabled = false;
                                    unmountToolStripMenuItem.Enabled = true;
                                    break;

                                case Sshfs.DriveStatus.Mounting:
                                    if (0 < MountingIDs.IndexOf(new Tuple<Guid, Guid>(server.ID, folder.ID))
                                        || ToMount.Contains(new Tuple<Guid,Guid>(server.ID, folder.ID) ))
                                    {
                                        folder.Status = Sshfs.DriveStatus.Mounted;
                                        mountToolStripMenuItem.Enabled = false;
                                        MountAnimatonStop();
                                    }
                                    else
                                    {
                                        MountAnimationStart();
                                        mountToolStripMenuItem.Enabled = true;
                                        unmountToolStripMenuItem.Enabled = false;
                                    }
                                    break;

                                default:
                                    MountAnimatonStop();
                                    mountToolStripMenuItem.Enabled = true;
                                    unmountToolStripMenuItem.Enabled = false;
                                    break;
                            }
                            
                            groupBox1.Enabled = false;
                            groupBox2.Enabled = true;
                            groupBox3.Enabled = !checkBox_folder_usedefaultaccound.Checked;
                        }
                        else
                        {
                            // write data in edit box
                            textBox_folder_entry.Text = null;
                            textBox_folder_password.Text = null;
                            textBox_folder_privat_key.Text = null;
                            textBox8_folder_username.Text = null;
                            textBox9_folder_remotedirectory.Text = null;
                            //checkBox_folder_usedefaultaccound.Checked = null;
                            // Laufwerksbuchstaben zuweisen, funktioniert so nicht :::FIXME:::
                            //comboBox_folder_driveletter.Text = null;

                            groupBox1.Enabled = false;
                            groupBox2.Enabled = false;
                            groupBox3.Enabled = false;

                            mountToolStripMenuItem.Enabled = false;
                            unmountToolStripMenuItem.Enabled = false;

                        }
                        
                    }
                    else gBox2Vis = true;
                    break;
                default: break;
            }
        }

        private void WindowExpand(){
            if (Expanded)
            {
                if (groupBox2.Visible)
                {
                    groupBox1.Visible = groupBox2.Visible = false;
                    gBox2Vis = true;
                }
                else groupBox1.Visible = false;
                treeView1.Width = groupBox1.Location.X + groupBox1.Size.Width - 15;
                Expanded = false;
                button_windowexpand.Text = "<";
            }
            else
            {
                if (gBox2Vis) groupBox1.Visible = groupBox2.Visible = true;
                else groupBox1.Visible = true;
                treeView1.Width = groupBox1.Location.X - 25;
                Expanded = true;
                button_windowexpand.Text = ">";
            }
        }

        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            editToolStripMenuItem.Enabled = true;
/*           
            if (treeView1.SelectedNode.Index == 0 && treeView1.SelectedNode.Level != 1)                                //Test für Serverinfo -> ListView
            {
               // treeView1.SelectedNode.Text = String.Format("Name: TestServer"+ Environment.NewLine + "IP: 127.0.0.1" + Environment.NewLine + "Note: Testing the new Multiline feature");
            }
            if (treeView1.SelectedNode.Index == 0 && treeView1.SelectedNode.Level == 1)                                //Test für Serverinfo -> ListView
            {
                mountToolStripMenuItem.Enabled = true;
                //treeView1.SelectedNode.Text = String.Format("Name: TestFolder" + Environment.NewLine + "Path: /" + Environment.NewLine + "Note: Testing the new Multiline feature");
            }
*/
            ServerFolderEdit();
        }

        private void toolStripMenuItem1_Click(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            groupBox1.Visible = false;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            WindowExpand();
        }


        private void editToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if (!Expanded) WindowExpand();
            ServerFolderEdit();
            textBox_server_name.Focus();
        }

        private void editToolStripMenuItem2_Click(object sender, EventArgs e)
        {
            if (!Expanded) WindowExpand();
            ServerFolderEdit(); 
            textBox_folder_entry.Focus();
        }


        private void checkBox1_CheckedChanged(object sender, EventArgs e)  //Use Default Account Checkbox
        {
            groupBox3.Enabled = !checkBox_folder_usedefaultaccound.Checked;
        }

        private void mountToolStripMenuItem1_Click(object sender, EventArgs e)
        {

        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)   //Opens Options + About Dialog
        {
            AboutForm About = new AboutForm();
            About.ShowDialog();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            UpdateTreeView();
            // Balloon tip for the systemtray
            FiSSH.BalloonTipText = "Application Minimized.";
            FiSSH.BalloonTipTitle = "FiSSH";
        }

        private void treeView1_DragDrop(object sender, DragEventArgs e)
        {
            // Retrieve the client coordinates of the drop location.
            Point targetPoint = treeView1.PointToClient(new Point(e.X, e.Y));

            // Retrieve the node at the drop location.
            TreeNode targetNode = treeView1.GetNodeAt(targetPoint);

            // Retrieve the node that was dragged.
            TreeNode draggedNode = (TreeNode)e.Data.GetData(typeof(TreeNode));

            // Confirm that the node at the drop location is not  
            // the dragged node or a descendant of the dragged node. 
            if (!draggedNode.Equals(targetNode) && !ContainsNode(draggedNode, targetNode))
            {
                // If it is a move operation, remove the node from its current  
                // location and add it to the node at the drop location. 
                if (e.Effect == DragDropEffects.Move)
                {   
                    if (targetNode != null && (draggedNode.Level > targetNode.Level)) 
                    { 
                        draggedNode.Remove(); targetNode.Nodes.Add(draggedNode); 
                    }
                    else 
                    {
                        if (targetNode == null && draggedNode.Level != 1) { draggedNode.Remove(); treeView1.Nodes.Add(draggedNode); }
                    }
                    
                }

                // If it is a copy operation, clone the dragged node  
                // and add it to the node at the drop location. 
                else if (e.Effect == DragDropEffects.Copy)
                {
                    targetNode.Nodes.Add((TreeNode)draggedNode.Clone());
                }

                // Expand the node at the location  
                // to show the dropped node.
                if (targetNode != null) targetNode.Expand();
                else { }
            }
        }

        private bool ContainsNode(TreeNode node1, TreeNode node2)
        {
            // Check the parent node of the second node. 
            if (node2 == null) return false;
            if (node2.Parent == null) return false;
            if (node2.Parent.Equals(node1)) return true;

            // If the parent node is not null or equal to the first node,  
            // call the ContainsNode method recursively using the parent of  
            // the second node. 
            return ContainsNode(node1, node2.Parent);
        } 

        private void treeView1_DragEnter(object sender, DragEventArgs e)
        {
            e.Effect = e.AllowedEffect;
        }

        private void treeView1_DragLeave(object sender, EventArgs e)
        {

        }

        private void treeView1_DragOver(object sender, DragEventArgs e)
        {
            // Retrieve the client coordinates of the mouse position.
            Point targetPoint = treeView1.PointToClient(new Point(e.X, e.Y));

            // Select the node at the mouse position.
            treeView1.SelectedNode = treeView1.GetNodeAt(targetPoint);
        }

        private void treeView1_ItemDrag(object sender, ItemDragEventArgs e)
        {
            // Move the dragged node when the left mouse button is used. 
            if (e.Button == MouseButtons.Left)
            {
                DoDragDrop(e.Item, DragDropEffects.Move);
            }

      
        }

        private void button2_Click(object sender, EventArgs e)
        {
            
        }

        private void optionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OptionsForm Options = new OptionsForm();
            Options.ShowDialog();
        }

        private void label8_Click(object sender, EventArgs e)
        {

        }

        private void label7_Click(object sender, EventArgs e)
        {

        }

        private void treeView1_DrawNode(object sender, DrawTreeNodeEventArgs e)
        {
            e.Graphics.DrawString(e.Node.Text, font, Brushes.Black, Rectangle.Inflate(e.Bounds, 2, 0));
        }

        private void radioButton6_CheckedChanged(object sender, EventArgs e)
        {
            textBox_folder_privat_key.Enabled = button4.Enabled = false;
            textBox_folder_password.Enabled = true;
        }

        private void radioButton5_CheckedChanged(object sender, EventArgs e)
        {
            textBox_folder_privat_key.Enabled = button4.Enabled = true;
            textBox_folder_password.Enabled = false;
        }

        private void radioButton4_CheckedChanged(object sender, EventArgs e)
        {
            textBox_folder_privat_key.Enabled = button4.Enabled = textBox_folder_password.Enabled = false;
        }

        private void radioButton1_CheckedChanged(object sender, EventArgs e)
        {
            textBox_server_privatkey.Enabled = button1.Enabled = false;
            textBox_server_password.Enabled = true;
        }

        private void radioButton2_CheckedChanged(object sender, EventArgs e)
        {
            textBox_server_privatkey.Enabled = button1.Enabled = true;
            textBox_server_password.Enabled = false;
        }

        private void radioButton3_CheckedChanged(object sender, EventArgs e)
        {
            textBox_server_privatkey.Enabled = button1.Enabled = textBox_server_password.Enabled = false;
        }

        private void radioButton7_CheckedChanged(object sender, EventArgs e)
        {
            comboBox_folder_driveletter.Enabled = true;
        }

        private void radioButton8_CheckedChanged(object sender, EventArgs e)
        {
            comboBox_folder_driveletter.Enabled = false;
        }

        private void button6_Click(object sender, EventArgs e)
        {
            //Add feature: button2.Enabled = false; while no changes are made -> Later
           
        }

        private void editToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!Expanded) WindowExpand();
            ServerFolderEdit();
            if (groupBox2.Visible) textBox_folder_entry.Focus();
            else textBox_server_name.Focus();
        }

        private void timer_animation_Tick(object sender, EventArgs e)
        {
            
            string[] Mounting = new string[4] {"Mounting","Mounting.","Mounting..","Mounting..."};
            mountToolStripMenuItem.Text = Mounting[TimerCount];
            mountToolStripMenuItem.Image = imageList2.Images[TimerCount];
            TimerCount++;
            if (TimerCount == 4)
            {
                TimerCount = 0;
            }
        }

        // Updates frequently all icons
        private void time_viewupdate_Tick(object sender, EventArgs e)
        {
            ServerFolderEdit();
        }



        private void mountToolStripMenuItem_Click_help()
        {
            Tuple<Guid, Guid> IDs = ToMount.Dequeue();
            MountingIDs.Add(IDs);
            //MountingFlagPipe.Enqueue(true);
            try
            {
                bone_server.Mount(IDs.Item1, IDs.Item2);
                ServerFolderEdit();
            }
            catch (FaultException<Fault> thrown_error)
            {
                //:::FIXME:::
                MessageBox.Show(thrown_error.Detail.Message);
            }
            catch (Exception thrown_error)
            {
                //:::FIXME:::
            }
            //MountingFlagPipe.Dequeue();
            MountingIDs.Remove(IDs);
        }

       private void mountToolStripMenuItem_Click(object sender, EventArgs e)
        {// Only folders can be mounted

            ServerModel server = GetSelectedServerNode();
            FolderModel folder = GetSelectedFolderNode();

            if (server == null || folder == null)
            {
                //:::FIXME:::
                return;
            }

          
            if (0 < MountingIDs.IndexOf(new Tuple<Guid, Guid>(server.ID, folder.ID))
                   || ToMount.Contains(new Tuple<Guid,Guid>(server.ID, folder.ID) ))
            {
                return;
            }

            ToMount.Enqueue(new Tuple<Guid, Guid>(server.ID,folder.ID));
            folder.Status = Sshfs.DriveStatus.Mounting;
            MountAnimationStart();

           this.MountThread =
                new System.Threading.Thread(new System.Threading.ThreadStart(this.mountToolStripMenuItem_Click_help));
           MountThread.Start();         
    
         }

        private void unmountToolStripMenuItem_Click(object sender, EventArgs e)
        {// Only folders can be mounted

            ServerModel server = GetSelectedServerNode();
            FolderModel folder = GetSelectedFolderNode();

            if (server == null || folder == null)
            {
                //:::FIXME:::
                return;
            }

            try
            {
                bone_server.UMount(server.ID, folder.ID);
                ServerFolderEdit();
            }
            catch (FaultException<Fault> thrown_error)
            {
                //:::FIXME:::
                MessageBox.Show(thrown_error.Detail.Message);
            }
            catch (Exception thrown_error)
            {
                //:::FIXME:::
            }

            // :::FIXME::: die animation muss parallel laufen 
            //loads the animation for mounting
            /*
            if (treeView1.SelectedNode.Index == 0 && treeView1.SelectedNode.Level != 0 && treeView1.SelectedNode.Level != 2 && timer1.Enabled == false)
            {
                deleteToolStripMenuItem.Enabled = false;
                editToolStripMenuItem.Enabled = false;
                optionsToolStripMenuItem.Enabled = false;
                timer1.Enabled =true;
            }
         //stops the animation and loads the mount image 
            else { timer1.Enabled = false;
                   deleteToolStripMenuItem.Enabled = true;
                   optionsToolStripMenuItem.Enabled = true;
                   mountToolStripMenuItem.Image = imageList1.Images[0];
                   mountToolStripMenuItem.Text = "Mount";

                 }*/
        }


        private void FiSSHForm_Resize(object sender, EventArgs e)
        {// Determine if the cursor is in the window
            bool cursorNotInBar = Screen.GetWorkingArea(this).Contains(Cursor.Position);
            if (this.WindowState == FormWindowState.Minimized && cursorNotInBar)
            {
               this.ShowInTaskbar = false;
               FiSSH.ShowBalloonTip(1000);
            }
        }

        private void restoreToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Normal;
            this.ShowInTaskbar = true;
        }

        private void closeApplicationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void restoreToolStripMenuItem_Click(object sender, MouseEventArgs e)
        {
            this.WindowState = FormWindowState.Normal;
            this.ShowInTaskbar = true;
        }

        private void FiSSHForm_Closing(object sender, FormClosingEventArgs e)
        { // The user has requested the form be closed so mimimize to the system tray instead
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.WindowState = FormWindowState.Minimized;
                this.ShowInTaskbar = false;
            }
        }


        #region TOOLS

        private ServerModel GetSelectedServerNode()
        {
            switch (treeView1.SelectedNode.Level)
            {
                case 0: //return datamodel.ElementAt(treeView1.SelectedNode.Index);
                    return datamodel.Find(x => treeView1.SelectedNode.Equals(x.gui_node));

                case 1: //return datamodel.ElementAt(treeView1.SelectedNode.Parent.Index);
                    return datamodel.Find(x => treeView1.SelectedNode.Parent.Equals(x.gui_node));

                default: return null;
            }
        }


        private FolderModel GetSelectedFolderNode()
        {
            switch (treeView1.SelectedNode.Level)
            {
                case 0: return null;

                case 1: //return datamodel.ElementAt(treeView1.SelectedNode.Parent.Index)
                        //            .Folders.ElementAt(treeView1.SelectedNode.Index);
                    ServerModel server = datamodel.Find(x => treeView1.SelectedNode.Parent.Equals(x.gui_node));
                    return server.Folders.Find(x => treeView1.SelectedNode.Equals(x.gui_node));

                default: return null;
            }
        }
 
        private void MountAnimationStart()
        {
            timer_animation.Enabled = true;
        }

        private void MountAnimatonStop()
        {
            timer_animation.Enabled = false;
            mountToolStripMenuItem.Image = imageList1.Images[0];
            mountToolStripMenuItem.Text = "Mount";
        }

        #endregion
    }
}