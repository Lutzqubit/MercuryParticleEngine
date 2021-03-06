﻿/*  
 Copyright © 2009 Project Mercury Team Members (http://mpe.codeplex.com/People/ProjectPeople.aspx)

 This program is licensed under the Microsoft Permissive License (Ms-PL).  You should 
 have received a copy of the license along with the source code.  If not, an online copy
 of the license can be found at http://mpe.codeplex.com/license.
*/


using BindingLibrary;
using WinFormsGraphicsDevice;

namespace ProjectMercury.EffectEditor
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.ComponentModel.Composition;
    using System.Diagnostics;
    using System.Drawing;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Windows.Forms;
    using ProjectMercury.EffectEditor.PluginInterfaces;
    using ProjectMercury.EffectEditor.TreeNodes;
    using ProjectMercury.Emitters;
    using ProjectMercury.Modifiers;
    using ProjectMercury.Renderers;

    [Export(typeof(IInterfaceProvider))]
    internal partial class UserInterface : Form, IInterfaceProvider
    {
        public event EventHandler Ready;

        protected virtual void OnReady(EventArgs e)
        {
            if (this.Ready != null)
                this.Ready(this, e);
        }

        public event SerializeEventHandler Serialize;

        protected virtual void OnSerialize(SerializeEventArgs e)
        {
            if (this.Serialize != null)
                this.Serialize(this, e);
        }

        public event SerializeEventHandler Deserialize;

        protected virtual void OnDeserialize(SerializeEventArgs e)
        {
            if (this.Deserialize != null)
                this.Deserialize(this, e);
        }

        public event NewEmitterEventHandler EmitterAdded;

        protected virtual void OnEmitterAdded(NewEmitterEventArgs e)
        {
            if (this.EmitterAdded != null)
                this.EmitterAdded(this, e);
        }

        public event CloneEmitterEventHandler EmitterCloned;

        protected virtual void OnEmitterCloned(CloneEmitterEventArgs e)
        {
            if (this.EmitterCloned != null)
                this.EmitterCloned(this, e);
        }

        public event EmitterEventHandler EmitterRemoved;

        protected virtual void OnEmitterRemoved(EmitterEventArgs e)
        {
            if (this.EmitterRemoved != null)
                this.EmitterRemoved(this, e);
        }

        public event NewModifierEventHandler ModifierAdded;

        protected virtual void OnModifierAdded(NewModifierEventArgs e)
        {
            if (this.ModifierAdded != null)
                this.ModifierAdded(this, e);
        }

        public event CloneModifierEventHandler ModifierCloned;

        protected virtual void OnModifierCloned(CloneModifierEventArgs e)
        {
            if (this.ModifierCloned != null)
                this.ModifierCloned(this, e);
        }

        public event ModifierEventHandler ModifierRemoved;

        protected virtual void OnModifierRemoved(ModifierEventArgs e)
        {
            if (this.ModifierRemoved != null)
                this.ModifierRemoved(this, e);
        }

        public event EmitterReinitialisedEventHandler EmitterReinitialised;

        protected virtual void OnEmitterReinitialised(EmitterReinitialisedEventArgs e)
        {
            if (this.EmitterReinitialised != null)
                this.EmitterReinitialised(this, e);
        }

        public event NewTextureReferenceEventHandler TextureReferenceAdded;

        protected virtual void OnTextureReferenceAdded(NewTextureReferenceEventArgs e)
        {
            if (this.TextureReferenceAdded != null)
                this.TextureReferenceAdded(this, e);
        }

        public event TextureReferenceChangedEventHandler TextureReferenceChanged;

        protected virtual void OnTextureReferenceChanged(TextureReferenceChangedEventArgs e)
        {
            if (this.TextureReferenceChanged != null)
                this.TextureReferenceChanged(this, e);
        }

        public event EventHandler NewParticleEffect;

        protected virtual void OnNewParticleEffect(EventArgs e)
        {
            if (this.NewParticleEffect != null)
                this.NewParticleEffect(this, e);
        }

        public IEnumerable<TextureReference> TextureReferences { get; set; }


        public void SetEmitterPlugins(IEnumerable<IEmitterPlugin> plugins)
        {
            foreach (IEmitterPlugin plugin in plugins)
                this.AddEmitterPlugin(plugin);
        }

        public void SetModifierPlugins(IEnumerable<IModifierPlugin> plugins)
        {
            foreach (IModifierPlugin plugin in plugins)
                this.AddModifierPlugin(plugin);
        }

        public void SetSerializationPlugins(IEnumerable<IEffectSerializationPlugin> plugins)
        {
            foreach (IEffectSerializationPlugin plugin in plugins)
                this.AddSerializationPlugin(plugin);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EffectEditorWindow"/> class.
        /// </summary>
        public UserInterface()
        {
            this.InitializeComponent();

            this.TriggerTimer = new Stopwatch();

        }

        /// <summary>
        /// Raises the <see cref="E:System.Windows.Forms.Form.Load"/> event.
        /// </summary>
        /// <param name="e">An <see cref="T:System.EventArgs"/> that contains the event data.</param>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            this.OnReady(EventArgs.Empty);

            this.DisplayLibraryEffects();
        }

        private bool MouseButtonPressed;

        private Point LocalMousePosition;

        private Color PreviewBackgroundColor = Color.Black;
        private ParticleEffect effect;

        /// <summary>
        /// Displays the library effects.
        /// </summary>
        private void DisplayLibraryEffects()
        {
            DirectoryInfo effectsDir = new DirectoryInfo(Application.StartupPath + "\\EffectLibrary");

            foreach (FileInfo file in effectsDir.GetFiles())
            {
                foreach (IEffectSerializationPlugin plugin in (from ToolStripItem menuItem in this.uxImportMenuItem.DropDownItems
                                                               where menuItem.Tag is IEffectSerializationPlugin
                                                               select menuItem.Tag as IEffectSerializationPlugin))
                {
                    if (plugin.FileFilter.Contains(file.Extension))
                    {
                        this.uxLibraryImageList.Images.Add(file.Name, plugin.DisplayIcon);

                        ListViewItem item = new ListViewItem
                        {
                            Text = file.Name,
                            Tag = plugin,
                            ImageIndex = this.uxLibraryImageList.Images.IndexOfKey(file.Name),
                            StateImageIndex = this.uxLibraryImageList.Images.IndexOfKey(file.Name)
                        };

                        this.uxLibraryListView.Items.Add(item);

                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Handles the Click event of the uxMainMenuToggleEffectTree control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void uxMainMenuToggleEffectTree_Click(object sender, EventArgs e)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                this.Invoke((MethodInvoker)delegate
                {
                    this.uxOuterSplitContainer.Panel1Collapsed = !this.uxMainMenuToggleEffectTree.Checked;
                });
            });
        }

        /// <summary>
        /// Handles the Click event of the uxMainMenuTogglePropertyBrowser control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void uxMainMenuTogglePropertyBrowser_Click(object sender, EventArgs e)
        {
            this.uxInnerSplitContainer.Panel2Collapsed = !this.uxMainMenuTogglePropertyBrowser.Checked;
        }

        /// <summary>
        /// Handles the AfterSelect event of the uxEffectTree control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.Forms.TreeViewEventArgs"/> instance containing the event data.</param>
        private void uxEffectTree_AfterSelect(object sender, TreeViewEventArgs e)
        {
            this.uxPropertyBrowser.SelectedObject = e.Node.Tag;
        }

        /// <summary>
        /// Handles the Click event of the uxMainMenuHomepage control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void uxMainMenuHomepage_Click(object sender, EventArgs e)
        {
            ThreadPool.QueueUserWorkItem(s => Process.Start("http://mpe.codeplex.com/"));
        }

        /// <summary>
        /// Handles the Click event of the uxMainMenuAbout control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void uxMainMenuAbout_Click(object sender, EventArgs e)
        {
            using (AboutWindow about = new AboutWindow())
            {
                about.ShowDialog();
            }
        }

        /// <summary>
        /// Handles the Click event of the uxMainMenuOptions control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void uxMainMenuOptions_Click(object sender, EventArgs e)
        {
            using (OptionsWindow options = new OptionsWindow())
            {
                options.BackgroundColor = this.PreviewBackgroundColor;

                options.BackgroundColourPickedCallback = new Action<Color>(delegate(Color color)
                    {
                        this.PreviewBackgroundColor = color;

                        this.uxEffectPreview.SetBackgroundColor(color.R, color.G, color.B);
                    });

                options.BackgroundImagePickedCallback = new Action<string>(delegate(string filePath)
                    {
                        this.uxEffectPreview.LoadBackgroundImage(filePath);
                    });

                options.BackgroundImageClearedCallback = new Action(delegate
                    {
                        this.uxEffectPreview.ClearBackgroundImage();
                    });

                options.BackgroundImageOptionsCallback = new Action<ImageOptions>(delegate(ImageOptions imageOptions)
                    {
                        this.uxEffectPreview.ImageOptionsChanged(imageOptions);
                    });

                options.ShowDialog();
            }
        }

        /// <summary>
        /// Handles the KeyUp event of the uxEffectTree control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.Forms.KeyEventArgs"/> instance containing the event data.</param>
        private void uxEffectTree_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                if (this.uxEffectTree.SelectedNode is EmitterTreeNode)
                {
                    Emitter emitter = (this.uxEffectTree.SelectedNode as EmitterTreeNode).Emitter;

                    this.OnEmitterRemoved(new EmitterEventArgs(emitter));
                }

                if (this.uxEffectTree.SelectedNode is ModifierTreeNode)
                {
                    Modifier modifier = (this.uxEffectTree.SelectedNode as ModifierTreeNode).Modifier;

                    this.OnModifierRemoved(new ModifierEventArgs(modifier));
                }

                this.uxEffectTree.SelectedNode.Remove();
            }
        }

        /// <summary>
        /// Handles the Click event of the uxMainMenuExit control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void uxMainMenuExit_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        /// Adds the copy plugin to the interface.
        /// </summary>
        /// <param name="plugin">The plugin.</param>
        private void AddEmitterPlugin(IEmitterPlugin plugin)
        {
            ToolStripMenuItem item = new ToolStripMenuItem
            {
                Text = plugin.DisplayName,
                ToolTipText = plugin.Description,
                Image = plugin.DisplayIcon.ToBitmap(),
                Tag = plugin
            };

            this.uxAddEmitterMenuItem.DropDownItems.Add(item);
        }

        /// <summary>
        /// Adds the modifier plugin to the interface.
        /// </summary>
        /// <param name="plugin">The plugin.</param>
        private void AddModifierPlugin(IModifierPlugin plugin)
        {
            ToolStripMenuItem item = new ToolStripMenuItem
            {
                Text = plugin.DisplayName,
                ToolTipText = plugin.Description,
                Image = plugin.DisplayIcon.ToBitmap(),
                Tag = plugin
            };

            bool foundCategory = false;

            foreach (ToolStripMenuItem categoryItem in this.uxAddModifierMenuItem.DropDownItems)
            {
                if (categoryItem.Text.Equals(plugin.Category))
                {
                    categoryItem.DropDownItems.Add(item);

                    foundCategory = true;
                }
            }

            if (!foundCategory)
            {
                ToolStripMenuItem newCategoryItem = new ToolStripMenuItem(plugin.Category);

                newCategoryItem.DropDownItemClicked += this.uxAddModifierMenuItem_DropDownItemClicked;

                this.uxAddModifierMenuItem.DropDownItems.Add(newCategoryItem);

                newCategoryItem.DropDownItems.Add(item);
            }

            //this.uxAddModifierMenuItem.DropDownItems.Add(item);
        }

        /// <summary>
        /// Adds the serialization plugin to the interface.
        /// </summary>
        /// <param name="plugin">The plugin.</param>
        private void AddSerializationPlugin(IEffectSerializationPlugin plugin)
        {
            //TODO: Perhaps it would be better to load the plugins and don't add them to the uxImportMenuItem
            //Instead we have a simple import button, and it has the filetypes with the supported plugins.
            //Example: .em and .pe (or something similar) are the only visible types in the import dialog.
            //Doing it this way will prevent loading of dynamic menu items and fix the bug where the menu items is
            //still displayed even tho the load dialog has been displayed.
            if (plugin.DeserializeSupported)
            {
                ToolStripMenuItem item = new ToolStripMenuItem
                {
                    Image = plugin.DisplayIcon.ToBitmap(),
                    Text = plugin.DisplayName,
                    ToolTipText = plugin.Description,
                    Tag = plugin
                };

                this.uxImportMenuItem.DropDownItems.Add(item);
            }

            if (plugin.SerializeSuported)
            {
                ToolStripMenuItem item = new ToolStripMenuItem
                {
                    Image = plugin.DisplayIcon.ToBitmap(),
                    Text = plugin.DisplayName,
                    ToolTipText = plugin.Description,
                    Tag = plugin
                };

                this.uxExportMenuItem.DropDownItems.Add(item);
            }
        }

        /// <summary>
        /// Sets the particle effect.
        /// </summary>
        /// <param name="effect">The effect.</param>
        public void SetParticleEffect(ParticleEffect effect)
        {
            this.effect = effect;
            
            this.uxEffectTree.Nodes.Clear();

            ParticleEffectTreeNode node = new ParticleEffectTreeNode(effect);

            this.uxEffectTree.Nodes.Add(node);

            this.uxEffectTree.SelectedNode = node;

            node.Expand();
        }



        /// <summary>
        /// Sets the amount of time it took to update the particle effect.
        /// </summary>
        /// <param name="totalSeconds"></param>
        public void SetUpdateTime(float totalSeconds)
        {
            this.uxUpdateTimeLabel.Text = String.Format("Update: {0:F5} seconds", totalSeconds);

            float framePercent = (totalSeconds / 0.016f) * 100f;

            framePercent = framePercent >= 100f ? 100f : framePercent;

            this.uxFrameRateProgress.Value = (int)framePercent;
        }

        /// <summary>
        /// Handles the DropDownItemClicked event of the uxImportMenuItem control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.Forms.ToolStripItemClickedEventArgs"/> instance containing the event data.</param>
        private void uxImportMenuItem_DropDownItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            var plugin = e.ClickedItem.Tag as IEffectSerializationPlugin;

            this.uxImportEffectDialog.Filter = plugin.FileFilter;

            if (this.uxImportEffectDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    var filePath = this.uxImportEffectDialog.FileName;

                    var args = new SerializeEventArgs(plugin, filePath);

                    this.OnDeserialize(args);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
        }

        /// <summary>
        /// Handles the DropDownItemClicked event of the uxExportMenuItem control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.Forms.ToolStripItemClickedEventArgs"/> instance containing the event data.</param>
        private void uxExportMenuItem_DropDownItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            var plugin = e.ClickedItem.Tag as IEffectSerializationPlugin;

            this.uxExportEffectDialog.Filter = plugin.FileFilter;

            if (this.uxExportEffectDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    var filePath = this.uxExportEffectDialog.FileName;

                    var args = new SerializeEventArgs(plugin, filePath);

                    this.OnSerialize(args);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
        }

        /// <summary>
        /// Draws the specified effect.
        /// </summary>
        /// <param name="effect">The effect.</param>
        /// <param name="renderer">The renderer.</param>
        public void Draw(ParticleEffect effect, Renderer renderer)
        {
            this.uxActiveParticlesLabel.Text = String.Format("{0} Active Particles", effect.ActiveParticlesCount);

            this.uxEffectPreview.ParticleEffect = effect;
            this.uxEffectPreview.Renderer = renderer;

            this.uxEffectPreview.Invalidate();
        }

        /// <summary>
        /// Handles the MouseDown event of the uxEffectPreview control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.Forms.MouseEventArgs"/> instance containing the event data.</param>
        private void uxEffectPreview_MouseDown(object sender, MouseEventArgs e)
        {
            this.MouseButtonPressed = true;

            this.LocalMousePosition = e.Location;

            this.uxStatusLabel.Text = this.LocalMousePosition.ToString();

            this.TriggerTimer.Start();
        }

        /// <summary>
        /// Handles the MouseUp event of the uxEffectPreview control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.Forms.MouseEventArgs"/> instance containing the event data.</param>
        private void uxEffectPreview_MouseUp(object sender, MouseEventArgs e)
        {
            this.MouseButtonPressed = false;

            this.uxStatusLabel.Text = "Ready";

            this.TriggerTimer.Stop();
        }

        /// <summary>
        /// Handles the MouseMove event of the uxEffectPreview control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.Forms.MouseEventArgs"/> instance containing the event data.</param>
        private void uxEffectPreview_MouseMove(object sender, MouseEventArgs e)
        {
            if (this.MouseButtonPressed)
            {
                this.LocalMousePosition = e.Location;

                this.uxStatusLabel.Text = this.LocalMousePosition.ToString();
            }
        }

        private Stopwatch TriggerTimer { get; set; }

        /// <summary>
        /// Gets a value indicating wether or not a trigger is required.
        /// </summary>
        /// <param name="x">The x location of the trigger.</param>
        /// <param name="y">The y location of the trigger.</param>
        public bool TriggerRequired(out float x, out float y)
        {
            x = (float)this.LocalMousePosition.X;
            y = (float)this.LocalMousePosition.Y;

            if (this.MouseButtonPressed)
            {
                if (this.TriggerTimer.Elapsed.TotalSeconds > (1f / 60f))
                {
                    this.TriggerTimer.Reset();
                    this.TriggerTimer.Start();

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Handles the Opening event of the uxEffectTreeContextMenu control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.ComponentModel.CancelEventArgs"/> instance containing the event data.</param>
        private void uxEffectTreeContextMenu_Opening(object sender, CancelEventArgs e)
        {
            TreeNode selectedNode = this.uxEffectTree.SelectedNode;

            this.uxAddEmitterMenuItem.Visible = selectedNode is ParticleEffectTreeNode;
            this.uxAddModifierMenuItem.Visible = selectedNode is EmitterTreeNode;
            this.uxReinitialiseEmitterMenuItem.Visible = selectedNode is EmitterTreeNode;
            this.uxSelectTextureMenuItem.Visible = selectedNode is EmitterTreeNode;
            this.uxToggleEmitterEnabledMenuItem.Visible = selectedNode is EmitterTreeNode;

            this.uxEffectTreeContextMenuSeperator.Visible = (selectedNode is ParticleEffectTreeNode) == false;
            this.uxDeleteMenuItem.Visible = (selectedNode is ParticleEffectTreeNode) == false;
            this.uxCloneMenuItem.Visible = (selectedNode is ParticleEffectTreeNode) == false;

            if (selectedNode is EmitterTreeNode)
            {
                Emitter emitter = (selectedNode as EmitterTreeNode).Emitter;

                this.uxToggleEmitterEnabledMenuItem.Text = emitter.Enabled ? "Disable" : "Enable";
            }
        }

        /// <summary>
        /// Handles the DropDownItemClicked event of the uxAddEmitterMenuItem control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.Forms.ToolStripItemClickedEventArgs"/> instance containing the event data.</param>
        private void uxAddEmitterMenuItem_DropDownItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            try
            {
                using (NewEmitterDialog dialog = new NewEmitterDialog())
                {
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        IEmitterPlugin plugin = e.ClickedItem.Tag as IEmitterPlugin;

                        var args = new NewEmitterEventArgs(plugin, dialog.EmitterBudget, dialog.EmitterTerm);

                        this.OnEmitterAdded(args);

                        if (args.AddedEmitter != null)
                        {
                            Emitter emitter = args.AddedEmitter;

                            EmitterTreeNode node = new EmitterTreeNode(emitter);

                            this.uxEffectTree.Nodes[0].Nodes.Add(node);

                            node.EnsureVisible();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        /// <summary>
        /// Handles the DropDownItemClicked event of the uxAddModifierMenuItem control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.Forms.ToolStripItemClickedEventArgs"/> instance containing the event data.</param>
        private void uxAddModifierMenuItem_DropDownItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            try
            {
                EmitterTreeNode parentNode = this.uxEffectTree.SelectedNode as EmitterTreeNode;

                IModifierPlugin plugin = e.ClickedItem.Tag as IModifierPlugin;

                Emitter parent = parentNode.Emitter;

                var args = new NewModifierEventArgs(parent, plugin);

                this.OnModifierAdded(args);

                if (args.AddedModifier != null)
                {
                    Modifier modifier = args.AddedModifier;

                    ModifierTreeNode node = new ModifierTreeNode(modifier);

                    parentNode.Nodes.Add(node);

                    node.EnsureVisible();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

        }

        /// <summary>
        /// Handles the Click event of the uxCloneMenuItem control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void uxCloneMenuItem_Click(object sender, EventArgs e)
        {
            var selectedNode = this.uxEffectTree.SelectedNode;

            if (selectedNode is EmitterTreeNode)
            {
                CloneEmitterEventArgs args = new CloneEmitterEventArgs((selectedNode as EmitterTreeNode).Emitter);

                this.OnEmitterCloned(args);

                if (args.AddedEmitter != null)
                {
                    EmitterTreeNode newNode = new EmitterTreeNode(args.AddedEmitter);

                    selectedNode.Parent.Nodes.Add(newNode);

                    newNode.Expand();

                    newNode.EnsureVisible();
                }
            }
            else if (selectedNode is ModifierTreeNode)
            {
                CloneModifierEventArgs args = new CloneModifierEventArgs((selectedNode as ModifierTreeNode).Modifier);

                this.OnModifierCloned(args);

                if (args.AddedModifier != null)
                {
                    ModifierTreeNode newNode = new ModifierTreeNode(args.AddedModifier);

                    selectedNode.Parent.Nodes.Add(newNode);

                    newNode.EnsureVisible();
                }
            }
        }

        /// <summary>
        /// Handles the Click event of the uxDeleteMenuItem control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void uxDeleteMenuItem_Click(object sender, EventArgs e)
        {
            var selectedNode = this.uxEffectTree.SelectedNode;

            if (selectedNode is EmitterTreeNode)
            {
                try
                {
                    Emitter emitter = (selectedNode as EmitterTreeNode).Emitter;

                    this.OnEmitterRemoved(new EmitterEventArgs(emitter));

                    selectedNode.Remove();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }

            else if (selectedNode is ModifierTreeNode)
            {
                try
                {
                    Modifier modifier = (selectedNode as ModifierTreeNode).Modifier;

                    this.OnModifierRemoved(new ModifierEventArgs(modifier));

                    selectedNode.Remove();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
        }

        /// <summary>
        /// Handles the Click event of the uxReinitialiseEmitter control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void uxReinitialiseEmitter_Click(object sender, EventArgs e)
        {
            EmitterTreeNode node = this.uxEffectTree.SelectedNode as EmitterTreeNode;

            using (NewEmitterDialog dialog = new NewEmitterDialog(node.Emitter.Budget, node.Emitter.Term))
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    var args = new EmitterReinitialisedEventArgs(node.Emitter, dialog.EmitterBudget, dialog.EmitterTerm);

                    this.OnEmitterReinitialised(args);
                }
            }
        }

        /// <summary>
        /// Handles the Click event of the uxMainMenuViewTextures control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void uxMainMenuViewTextures_Click(object sender, EventArgs e)
        {
            using (TextureReferenceBrowser browser = new TextureReferenceBrowser(this.TextureReferences, this.TextureReferenceAdded))
            {
                browser.ShowDialog();
            }
        }

        /// <summary>
        /// Handles the Click event of the uxSelectTexture control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void uxSelectTexture_Click(object sender, EventArgs e)
        {
            using (TextureReferenceBrowser browser = new TextureReferenceBrowser(this.TextureReferences, this.TextureReferenceAdded))
            {
                if (browser.ShowDialog() == DialogResult.OK)
                {
                    EmitterTreeNode node = this.uxEffectTree.SelectedNode as EmitterTreeNode;

                    var args = new TextureReferenceChangedEventArgs(node.Emitter, browser.SelectedReference);

                    this.OnTextureReferenceChanged(args);

                    this.uxPropertyBrowser.Refresh();
                }
            }
        }

        /// <summary>
        /// Handles the ItemActivate event of the uxLibraryListView control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void uxLibraryListView_ItemActivate(object sender, EventArgs e)
        {
            if (MessageBox.Show("Are you sure you wish to open this effect? Unsaved changes will be lost.",
                                "Confirm",
                                MessageBoxButtons.YesNoCancel,
                                MessageBoxIcon.Question) == DialogResult.Yes)
            {
                ListViewItem item = uxLibraryListView.SelectedItems[0];

                IEffectSerializationPlugin plugin = item.Tag as IEffectSerializationPlugin;

                string filePath = Application.StartupPath + "\\EffectLibrary\\" + item.Text;

                this.OnDeserialize(new SerializeEventArgs(plugin, filePath));
            }
        }

        /// <summary>
        /// Handles the Click event of the uxMainMenuNew control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void uxMainMenuNew_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Are you sure you wish to start a new effect? Unsaved changed will be lost.",
                                "Confirm",
                                MessageBoxButtons.YesNoCancel,
                                MessageBoxIcon.Question) == DialogResult.Yes)
            {
                this.OnNewParticleEffect(EventArgs.Empty);
            }
        }

        /// <summary>
        /// Handles the Click event of the uxMainMenuDocumentation control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void uxMainMenuDocumentation_Click(object sender, EventArgs e)
        {
            ThreadPool.QueueUserWorkItem(s => Process.Start("http://mpe.codeplex.com/documentation"));
        }

        /// <summary>
        /// Handles the Click event of the uxAPIReferenceMenuItem control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void uxAPIReferenceMenuItem_Click(object sender, EventArgs e)
        {
            ThreadPool.QueueUserWorkItem(s => Process.Start(".\\Reference\\Documentation.chm"));
        }

        /// <summary>
        /// Handles the CheckedChanged event of the uxToggleEmitterEnabledMenuItem control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void uxToggleEmitterEnabledMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            Emitter emitter = (this.uxEffectTree.SelectedNode as EmitterTreeNode).Emitter;

            if (emitter.Enabled == false)
            {
                this.uxEffectTree.SelectedNode.ForeColor = SystemColors.WindowText;
                
                this.uxEffectTree.SelectedNode.Text = this.uxEffectTree.SelectedNode.Text.Replace(" (Disabled)", "");
                
                this.uxEffectTree.SelectedNode.Expand();

                emitter.Enabled = true;
            }
            else
            {
                this.uxEffectTree.SelectedNode.Collapse();

                this.uxEffectTree.SelectedNode.ForeColor = Color.Gray;
                
                this.uxEffectTree.SelectedNode.Text = String.Format("{0} (Disabled)", this.uxEffectTree.SelectedNode.Text);

                emitter.Enabled = false;
            }
        }

        private void uxMainMenuViewBinding_Click(object sender, EventArgs e)
        {

            using (BindingForm about = new BindingForm())
            {


                about.ParticleEffect = effect;


                about.ShowDialog();
            }
        }
    }
}
