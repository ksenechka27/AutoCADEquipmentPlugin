namespace AutoCADEquipmentPlugin.UI
{
    partial class PlaceForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.TextBox txtBlockName;
        private System.Windows.Forms.NumericUpDown numOffset;
        private System.Windows.Forms.Label lblBlockName;
        private System.Windows.Forms.Label lblOffset;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Button btnCancel;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.txtBlockName = new System.Windows.Forms.TextBox();
            this.numOffset = new System.Windows.Forms.NumericUpDown();
            this.lblBlockName = new System.Windows.Forms.Label();
            this.lblOffset = new System.Windows.Forms.Label();
            this.btnOk = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.numOffset)).BeginInit();
            this.SuspendLayout();

            // txtBlockName
            this.txtBlockName.Location = new System.Drawing.Point(120, 20);
            this.txtBlockName.Name = "txtBlockName";
            this.txtBlockName.Size = new System.Drawing.Size(200, 23);

            // numOffset
            this.numOffset.Location = new System.Drawing.Point(120, 60);
            this.numOffset.Maximum = new decimal(new int[] { 1000, 0, 0, 0 });
            this.numOffset.Minimum = new decimal(new int[] { 0, 0, 0, 0 });
            this.numOffset.Name = "numOffset";
            this.numOffset.Size = new System.Drawing.Size(200, 23);
            this.numOffset.Value = new decimal(new int[] { 100, 0, 0, 0 });

            // lblBlockName
            this.lblBlockName.AutoSize = true;
            this.lblBlockName.Location = new System.Drawing.Point(20, 23);
            this.lblBlockName.Name = "lblBlockName";
            this.lblBlockName.Size = new System.Drawing.Size(90, 15);
            this.lblBlockName.Text = "Имя блока:";

            // lblOffset
            this.lblOffset.AutoSize = true;
            this.lblOffset.Location = new System.Drawing.Point(20, 62);
            this.lblOffset.Name = "lblOffset";
            this.lblOffset.Size = new System.Drawing.Size(74, 15);
            this.lblOffset.Text = "Отступ (мм):";

            // btnOk
            this.btnOk.Location = new System.Drawing.Point(120, 100);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(90, 30);
            this.btnOk.Text = "OK";
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);

            // btnCancel
            this.btnCancel.Location = new System.Drawing.Point(230, 100);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(90, 30);
            this.btnCancel.Text = "Отмена";
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);

            // PlaceForm
            this.AcceptButton = this.btnOk;
            this.CancelButton = this.btnCancel;
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.ClientSize = new System.Drawing.Size(350, 150);
            this.Controls.Add(this.txtBlockName);
            this.Controls.Add(this.numOffset);
            this.Controls.Add(this.lblBlockName);
            this.Controls.Add(this.lblOffset);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.btnCancel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "PlaceForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Параметры размещения";
            ((System.ComponentModel.ISupportInitialize)(this.numOffset)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}
