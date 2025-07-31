namespace AutoCADEquipmentPlugin.UI
{
    partial class PlaceForm
    {
        private System.ComponentModel.IContainer components = null;

        private System.Windows.Forms.Label lblBlockName;
        private System.Windows.Forms.TextBox txtBlockName;
        private System.Windows.Forms.Button btnPlace;
        private System.Windows.Forms.Button btnCancel;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.lblBlockName = new System.Windows.Forms.Label();
            this.txtBlockName = new System.Windows.Forms.TextBox();
            this.btnPlace = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.SuspendLayout();

            // lblBlockName
            this.lblBlockName.AutoSize = true;
            this.lblBlockName.Location = new System.Drawing.Point(12, 15);
            this.lblBlockName.Name = "lblBlockName";
            this.lblBlockName.Size = new System.Drawing.Size(92, 13);
            this.lblBlockName.TabIndex = 0;
            this.lblBlockName.Text = "Имя блока:";

            // txtBlockName
            this.txtBlockName.Location = new System.Drawing.Point(110, 12);
            this.txtBlockName.Name = "txtBlockName";
            this.txtBlockName.Size = new System.Drawing.Size(200, 20);
            this.txtBlockName.TabIndex = 1;

            // btnPlace
            this.btnPlace.Location = new System.Drawing.Point(110, 50);
            this.btnPlace.Name = "btnPlace";
            this.btnPlace.Size = new System.Drawing.Size(95, 23);
            this.btnPlace.TabIndex = 2;
            this.btnPlace.Text = "Разместить";
            this.btnPlace.UseVisualStyleBackColor = true;
            this.btnPlace.Click += new System.EventHandler(this.btnPlace_Click);

            // btnCancel
            this.btnCancel.Location = new System.Drawing.Point(215, 50);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(95, 23);
            this.btnCancel.TabIndex = 3;
            this.btnCancel.Text = "Отмена";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);

            // PlaceForm
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(330, 90);
            this.Controls.Add(this.lblBlockName);
            this.Controls.Add(this.txtBlockName);
            this.Controls.Add(this.btnPlace);
            this.Controls.Add(this.btnCancel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "PlaceForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Размещение оборудования";
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}
