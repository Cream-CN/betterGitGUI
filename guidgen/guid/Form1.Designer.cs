namespace guid
{
	partial class Form1
	{
		/// <summary>
		/// 必需的设计器变量。
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// 清理所有正在使用的资源。
		/// </summary>
		/// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing && (components != null))
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Windows 窗体设计器生成的代码

		/// <summary>
		/// 设计器支持所需的方法 - 不要修改
		/// 使用代码编辑器修改此方法的内容。
		/// </summary>
		private void InitializeComponent()
		{
			this.lblTitle = new System.Windows.Forms.Label();
			this.lblCount = new System.Windows.Forms.Label();
			this.lblName = new System.Windows.Forms.Label();
			this.txtName = new System.Windows.Forms.TextBox();
			this.chkUpper = new System.Windows.Forms.CheckBox();
			this.btnGenerate = new System.Windows.Forms.Button();
			this.btnCopyAll = new System.Windows.Forms.Button();
			this.lblOleCreate = new System.Windows.Forms.Label();
			this.txtOleCreate = new System.Windows.Forms.TextBox();
			this.btnCopyOleCreate = new System.Windows.Forms.Button();
			this.lblDefineGuid = new System.Windows.Forms.Label();
			this.txtDefineGuid = new System.Windows.Forms.TextBox();
			this.btnCopyDefineGuid = new System.Windows.Forms.Button();
			this.lblStaticConst = new System.Windows.Forms.Label();
			this.txtStaticConst = new System.Windows.Forms.TextBox();
			this.btnCopyStaticConst = new System.Windows.Forms.Button();
			this.lblRegistry = new System.Windows.Forms.Label();
			this.txtRegistry = new System.Windows.Forms.TextBox();
			this.btnCopyRegistry = new System.Windows.Forms.Button();
			this.lblBracket = new System.Windows.Forms.Label();
			this.txtBracket = new System.Windows.Forms.TextBox();
			this.btnCopyBracket = new System.Windows.Forms.Button();
			this.lblAngle = new System.Windows.Forms.Label();
			this.txtAngle = new System.Windows.Forms.TextBox();
			this.btnCopyAngle = new System.Windows.Forms.Button();
			this.lblStatus = new System.Windows.Forms.Label();
			this.linkAbout = new System.Windows.Forms.LinkLabel();
			this.SuspendLayout();
			// 
			// lblTitle
			// 
			this.lblTitle.AutoSize = true;
			this.lblTitle.Font = new System.Drawing.Font("Microsoft YaHei UI", 13.5F, System.Drawing.FontStyle.Bold);
			this.lblTitle.Location = new System.Drawing.Point(12, 9);
			this.lblTitle.Name = "lblTitle";
			this.lblTitle.Size = new System.Drawing.Size(119, 25);
			this.lblTitle.TabIndex = 0;
			this.lblTitle.Text = "GUID 生成器";
			// 
			// lblCount
			// 
			this.lblCount.ForeColor = System.Drawing.SystemColors.GrayText;
			this.lblCount.Location = new System.Drawing.Point(228, 17);
			this.lblCount.Name = "lblCount";
			this.lblCount.Size = new System.Drawing.Size(160, 17);
			this.lblCount.TabIndex = 1;
			this.lblCount.Text = "已生成 0 个";
			this.lblCount.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// lblName
			// 
			this.lblName.Location = new System.Drawing.Point(12, 41);
			this.lblName.Name = "lblName";
			this.lblName.Size = new System.Drawing.Size(38, 20);
			this.lblName.TabIndex = 2;
			this.lblName.Text = "名称";
			this.lblName.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			// 
			// txtName
			// 
			this.txtName.Location = new System.Drawing.Point(54, 39);
			this.txtName.Name = "txtName";
			this.txtName.Size = new System.Drawing.Size(146, 23);
			this.txtName.TabIndex = 3;
			this.txtName.TextChanged += new System.EventHandler(this.txtName_TextChanged);
			// 
			// chkUpper
			// 
			this.chkUpper.Location = new System.Drawing.Point(210, 39);
			this.chkUpper.Name = "chkUpper";
			this.chkUpper.Size = new System.Drawing.Size(62, 23);
			this.chkUpper.TabIndex = 4;
			this.chkUpper.Text = "大写";
			this.chkUpper.UseVisualStyleBackColor = true;
			this.chkUpper.CheckedChanged += new System.EventHandler(this.chkUpper_CheckedChanged);
			// 
			// btnGenerate
			// 
			this.btnGenerate.Location = new System.Drawing.Point(12, 70);
			this.btnGenerate.Name = "btnGenerate";
			this.btnGenerate.Size = new System.Drawing.Size(186, 33);
			this.btnGenerate.TabIndex = 5;
			this.btnGenerate.Text = "生成新 GUID（F5）";
			this.btnGenerate.UseVisualStyleBackColor = true;
			this.btnGenerate.Click += new System.EventHandler(this.btnGenerate_Click);
			// 
			// btnCopyAll
			// 
			this.btnCopyAll.Location = new System.Drawing.Point(202, 70);
			this.btnCopyAll.Name = "btnCopyAll";
			this.btnCopyAll.Size = new System.Drawing.Size(186, 33);
			this.btnCopyAll.TabIndex = 6;
			this.btnCopyAll.Text = "复制全部";
			this.btnCopyAll.UseVisualStyleBackColor = true;
			this.btnCopyAll.Click += new System.EventHandler(this.btnCopyAll_Click);
			// 
			// lblOleCreate
			// 
			this.lblOleCreate.Location = new System.Drawing.Point(12, 110);
			this.lblOleCreate.Name = "lblOleCreate";
			this.lblOleCreate.Size = new System.Drawing.Size(376, 16);
			this.lblOleCreate.TabIndex = 7;
			this.lblOleCreate.Text = "IMPLEMENT_OLECREATE（COM 类工厂）";
			// 
			// txtOleCreate
			// 
			this.txtOleCreate.BackColor = System.Drawing.SystemColors.Window;
			this.txtOleCreate.Font = new System.Drawing.Font("Consolas", 9F);
			this.txtOleCreate.Location = new System.Drawing.Point(12, 127);
			this.txtOleCreate.Name = "txtOleCreate";
			this.txtOleCreate.ReadOnly = true;
			this.txtOleCreate.Size = new System.Drawing.Size(306, 22);
			this.txtOleCreate.TabIndex = 8;
			this.txtOleCreate.DoubleClick += new System.EventHandler(this.ValueBox_DoubleClick);
			this.txtOleCreate.Enter += new System.EventHandler(this.ValueBox_Enter);
			// 
			// btnCopyOleCreate
			// 
			this.btnCopyOleCreate.Cursor = System.Windows.Forms.Cursors.Hand;
			this.btnCopyOleCreate.Location = new System.Drawing.Point(322, 126);
			this.btnCopyOleCreate.Name = "btnCopyOleCreate";
			this.btnCopyOleCreate.Size = new System.Drawing.Size(66, 24);
			this.btnCopyOleCreate.TabIndex = 9;
			this.btnCopyOleCreate.Tag = this.txtOleCreate;
			this.btnCopyOleCreate.Text = "复制";
			this.btnCopyOleCreate.UseVisualStyleBackColor = true;
			this.btnCopyOleCreate.Click += new System.EventHandler(this.CopyButton_Click);
			// 
			// lblDefineGuid
			// 
			this.lblDefineGuid.Location = new System.Drawing.Point(12, 171);
			this.lblDefineGuid.Name = "lblDefineGuid";
			this.lblDefineGuid.Size = new System.Drawing.Size(376, 16);
			this.lblDefineGuid.TabIndex = 10;
			this.lblDefineGuid.Text = "DEFINE_GUID（头文件常量）";
			// 
			// txtDefineGuid
			// 
			this.txtDefineGuid.BackColor = System.Drawing.SystemColors.Window;
			this.txtDefineGuid.Font = new System.Drawing.Font("Consolas", 9F);
			this.txtDefineGuid.Location = new System.Drawing.Point(12, 188);
			this.txtDefineGuid.Name = "txtDefineGuid";
			this.txtDefineGuid.ReadOnly = true;
			this.txtDefineGuid.Size = new System.Drawing.Size(306, 22);
			this.txtDefineGuid.TabIndex = 11;
			this.txtDefineGuid.DoubleClick += new System.EventHandler(this.ValueBox_DoubleClick);
			this.txtDefineGuid.Enter += new System.EventHandler(this.ValueBox_Enter);
			// 
			// btnCopyDefineGuid
			// 
			this.btnCopyDefineGuid.Cursor = System.Windows.Forms.Cursors.Hand;
			this.btnCopyDefineGuid.Location = new System.Drawing.Point(322, 187);
			this.btnCopyDefineGuid.Name = "btnCopyDefineGuid";
			this.btnCopyDefineGuid.Size = new System.Drawing.Size(66, 24);
			this.btnCopyDefineGuid.TabIndex = 12;
			this.btnCopyDefineGuid.Tag = this.txtDefineGuid;
			this.btnCopyDefineGuid.Text = "复制";
			this.btnCopyDefineGuid.UseVisualStyleBackColor = true;
			this.btnCopyDefineGuid.Click += new System.EventHandler(this.CopyButton_Click);
			// 
			// lblStaticConst
			// 
			this.lblStaticConst.Location = new System.Drawing.Point(12, 232);
			this.lblStaticConst.Name = "lblStaticConst";
			this.lblStaticConst.Size = new System.Drawing.Size(376, 16);
			this.lblStaticConst.TabIndex = 13;
			this.lblStaticConst.Text = "静态常量结构 static const GUID";
			// 
			// txtStaticConst
			// 
			this.txtStaticConst.BackColor = System.Drawing.SystemColors.Window;
			this.txtStaticConst.Font = new System.Drawing.Font("Consolas", 9F);
			this.txtStaticConst.Location = new System.Drawing.Point(12, 249);
			this.txtStaticConst.Name = "txtStaticConst";
			this.txtStaticConst.ReadOnly = true;
			this.txtStaticConst.Size = new System.Drawing.Size(306, 22);
			this.txtStaticConst.TabIndex = 14;
			this.txtStaticConst.DoubleClick += new System.EventHandler(this.ValueBox_DoubleClick);
			this.txtStaticConst.Enter += new System.EventHandler(this.ValueBox_Enter);
			// 
			// btnCopyStaticConst
			// 
			this.btnCopyStaticConst.Cursor = System.Windows.Forms.Cursors.Hand;
			this.btnCopyStaticConst.Location = new System.Drawing.Point(322, 248);
			this.btnCopyStaticConst.Name = "btnCopyStaticConst";
			this.btnCopyStaticConst.Size = new System.Drawing.Size(66, 24);
			this.btnCopyStaticConst.TabIndex = 15;
			this.btnCopyStaticConst.Tag = this.txtStaticConst;
			this.btnCopyStaticConst.Text = "复制";
			this.btnCopyStaticConst.UseVisualStyleBackColor = true;
			this.btnCopyStaticConst.Click += new System.EventHandler(this.CopyButton_Click);
			// 
			// lblRegistry
			// 
			this.lblRegistry.Location = new System.Drawing.Point(12, 293);
			this.lblRegistry.Name = "lblRegistry";
			this.lblRegistry.Size = new System.Drawing.Size(376, 16);
			this.lblRegistry.TabIndex = 16;
			this.lblRegistry.Text = "注册表格式 {…}";
			// 
			// txtRegistry
			// 
			this.txtRegistry.BackColor = System.Drawing.SystemColors.Window;
			this.txtRegistry.Font = new System.Drawing.Font("Consolas", 9F);
			this.txtRegistry.Location = new System.Drawing.Point(12, 310);
			this.txtRegistry.Name = "txtRegistry";
			this.txtRegistry.ReadOnly = true;
			this.txtRegistry.Size = new System.Drawing.Size(306, 22);
			this.txtRegistry.TabIndex = 17;
			this.txtRegistry.DoubleClick += new System.EventHandler(this.ValueBox_DoubleClick);
			this.txtRegistry.Enter += new System.EventHandler(this.ValueBox_Enter);
			// 
			// btnCopyRegistry
			// 
			this.btnCopyRegistry.Cursor = System.Windows.Forms.Cursors.Hand;
			this.btnCopyRegistry.Location = new System.Drawing.Point(322, 309);
			this.btnCopyRegistry.Name = "btnCopyRegistry";
			this.btnCopyRegistry.Size = new System.Drawing.Size(66, 24);
			this.btnCopyRegistry.TabIndex = 18;
			this.btnCopyRegistry.Tag = this.txtRegistry;
			this.btnCopyRegistry.Text = "复制";
			this.btnCopyRegistry.UseVisualStyleBackColor = true;
			this.btnCopyRegistry.Click += new System.EventHandler(this.CopyButton_Click);
			// 
			// lblBracket
			// 
			this.lblBracket.Location = new System.Drawing.Point(12, 354);
			this.lblBracket.Name = "lblBracket";
			this.lblBracket.Size = new System.Drawing.Size(376, 16);
			this.lblBracket.TabIndex = 19;
			this.lblBracket.Text = "方括号 [GUID(\"...\")]";
			// 
			// txtBracket
			// 
			this.txtBracket.BackColor = System.Drawing.SystemColors.Window;
			this.txtBracket.Font = new System.Drawing.Font("Consolas", 9F);
			this.txtBracket.Location = new System.Drawing.Point(12, 371);
			this.txtBracket.Name = "txtBracket";
			this.txtBracket.ReadOnly = true;
			this.txtBracket.Size = new System.Drawing.Size(306, 22);
			this.txtBracket.TabIndex = 20;
			this.txtBracket.DoubleClick += new System.EventHandler(this.ValueBox_DoubleClick);
			this.txtBracket.Enter += new System.EventHandler(this.ValueBox_Enter);
			// 
			// btnCopyBracket
			// 
			this.btnCopyBracket.Cursor = System.Windows.Forms.Cursors.Hand;
			this.btnCopyBracket.Location = new System.Drawing.Point(322, 370);
			this.btnCopyBracket.Name = "btnCopyBracket";
			this.btnCopyBracket.Size = new System.Drawing.Size(66, 24);
			this.btnCopyBracket.TabIndex = 21;
			this.btnCopyBracket.Tag = this.txtBracket;
			this.btnCopyBracket.Text = "复制";
			this.btnCopyBracket.UseVisualStyleBackColor = true;
			this.btnCopyBracket.Click += new System.EventHandler(this.CopyButton_Click);
			// 
			// lblAngle
			// 
			this.lblAngle.Location = new System.Drawing.Point(12, 415);
			this.lblAngle.Name = "lblAngle";
			this.lblAngle.Size = new System.Drawing.Size(376, 16);
			this.lblAngle.TabIndex = 22;
			this.lblAngle.Text = "尖括号 <GUID(\"...\")>";
			// 
			// txtAngle
			// 
			this.txtAngle.BackColor = System.Drawing.SystemColors.Window;
			this.txtAngle.Font = new System.Drawing.Font("Consolas", 9F);
			this.txtAngle.Location = new System.Drawing.Point(12, 432);
			this.txtAngle.Name = "txtAngle";
			this.txtAngle.ReadOnly = true;
			this.txtAngle.Size = new System.Drawing.Size(306, 22);
			this.txtAngle.TabIndex = 23;
			this.txtAngle.DoubleClick += new System.EventHandler(this.ValueBox_DoubleClick);
			this.txtAngle.Enter += new System.EventHandler(this.ValueBox_Enter);
			// 
			// btnCopyAngle
			// 
			this.btnCopyAngle.Cursor = System.Windows.Forms.Cursors.Hand;
			this.btnCopyAngle.Location = new System.Drawing.Point(322, 431);
			this.btnCopyAngle.Name = "btnCopyAngle";
			this.btnCopyAngle.Size = new System.Drawing.Size(66, 24);
			this.btnCopyAngle.TabIndex = 24;
			this.btnCopyAngle.Tag = this.txtAngle;
			this.btnCopyAngle.Text = "复制";
			this.btnCopyAngle.UseVisualStyleBackColor = true;
			this.btnCopyAngle.Click += new System.EventHandler(this.CopyButton_Click);
			// 
			// lblStatus
			// 
			this.lblStatus.ForeColor = System.Drawing.SystemColors.GrayText;
			this.lblStatus.Location = new System.Drawing.Point(12, 464);
			this.lblStatus.Name = "lblStatus";
			this.lblStatus.Size = new System.Drawing.Size(318, 18);
			this.lblStatus.TabIndex = 25;
			this.lblStatus.Text = "双击文本框可复制 · F5 重新生成";
			this.lblStatus.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			// 
			// linkAbout
			// 
			this.linkAbout.LinkBehavior = System.Windows.Forms.LinkBehavior.HoverUnderline;
			this.linkAbout.Location = new System.Drawing.Point(332, 464);
			this.linkAbout.Name = "linkAbout";
			this.linkAbout.Size = new System.Drawing.Size(44, 18);
			this.linkAbout.TabIndex = 26;
			this.linkAbout.TabStop = true;
			this.linkAbout.Text = "关于";
			this.linkAbout.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			this.linkAbout.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkAbout_LinkClicked);
			// 
			// Form1
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
			this.ClientSize = new System.Drawing.Size(400, 500);
			this.Controls.Add(this.lblStatus);
			this.Controls.Add(this.linkAbout);
			this.Controls.Add(this.btnCopyAngle);
			this.Controls.Add(this.txtAngle);
			this.Controls.Add(this.lblAngle);
			this.Controls.Add(this.btnCopyBracket);
			this.Controls.Add(this.txtBracket);
			this.Controls.Add(this.lblBracket);
			this.Controls.Add(this.btnCopyRegistry);
			this.Controls.Add(this.txtRegistry);
			this.Controls.Add(this.lblRegistry);
			this.Controls.Add(this.btnCopyStaticConst);
			this.Controls.Add(this.txtStaticConst);
			this.Controls.Add(this.lblStaticConst);
			this.Controls.Add(this.btnCopyDefineGuid);
			this.Controls.Add(this.txtDefineGuid);
			this.Controls.Add(this.lblDefineGuid);
			this.Controls.Add(this.btnCopyOleCreate);
			this.Controls.Add(this.txtOleCreate);
			this.Controls.Add(this.lblOleCreate);
			this.Controls.Add(this.btnCopyAll);
			this.Controls.Add(this.btnGenerate);
			this.Controls.Add(this.chkUpper);
			this.Controls.Add(this.txtName);
			this.Controls.Add(this.lblName);
			this.Controls.Add(this.lblCount);
			this.Controls.Add(this.lblTitle);
			this.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
			this.MaximizeBox = false;
			this.Name = "Form1";
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "GUID 生成器";
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.Label lblTitle;
		private System.Windows.Forms.Label lblCount;
		private System.Windows.Forms.Label lblName;
		private System.Windows.Forms.TextBox txtName;
		private System.Windows.Forms.CheckBox chkUpper;
		private System.Windows.Forms.Button btnGenerate;
		private System.Windows.Forms.Button btnCopyAll;
		private System.Windows.Forms.Label lblOleCreate;
		private System.Windows.Forms.TextBox txtOleCreate;
		private System.Windows.Forms.Button btnCopyOleCreate;
		private System.Windows.Forms.Label lblDefineGuid;
		private System.Windows.Forms.TextBox txtDefineGuid;
		private System.Windows.Forms.Button btnCopyDefineGuid;
		private System.Windows.Forms.Label lblStaticConst;
		private System.Windows.Forms.TextBox txtStaticConst;
		private System.Windows.Forms.Button btnCopyStaticConst;
		private System.Windows.Forms.Label lblRegistry;
		private System.Windows.Forms.TextBox txtRegistry;
		private System.Windows.Forms.Button btnCopyRegistry;
		private System.Windows.Forms.Label lblBracket;
		private System.Windows.Forms.TextBox txtBracket;
		private System.Windows.Forms.Button btnCopyBracket;
		private System.Windows.Forms.Label lblAngle;
		private System.Windows.Forms.TextBox txtAngle;
		private System.Windows.Forms.Button btnCopyAngle;
		private System.Windows.Forms.LinkLabel linkAbout;
		private System.Windows.Forms.Label lblStatus;
	}
}
