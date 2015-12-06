using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

/*
 * 問題点
 * 　例外処理などを入れていないのでどうなるかわからない・・・
 * 　全ソースべた書きなので読みづらい．
 * 
 * 既知のバグ：
 * dataGridViewの初期化部を2回呼ぶと落ちる(新しいCSVを開くときは1回閉じる必要あり)
 * ファイル保存を行うと内部変数を書き換えてしまうので，複数回ボタンを押すとデータがおかしくなる
 * 
 * 
 * 修正履歴
 * 　2010/10/03 
 * 　　・改行コードをCR+LFに変更
 * 　　・CSV出力の"を削除(区切りはカンマのみ)
 * 　2010/10/05
 * 　　・回転の処理を追加 X反転時には(540-θ)%360，y反転時には360-θ
 * 　　・ウィンドウの表示言語を英語に変更
 * 　　・Saveボタンを2回押せないように変更
 * 　2013/03/17
 * 　  ・最大化でレイアウトが崩れていたのを改善
 * 　  ・回転をXY同時に選択できないように変更(CheckBox -> RadioButtonに変更)
 * 　　
 */


namespace AltiumPickAndPlace_DataConverter
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }
        // CSVファイル全体を保存
        List<string[]> dataList;
        // 分解・パースしたデータを保持
        List<PickPlace> topPick;
        List<PickPlace> bottomPick;

        // 保存時のためにファイルパスを保存しておく
        string openedFilePath;

        /// <summary>
        /// CSV読み込み
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LoadData_ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Altium Pick and Place file(*.csv)|*.csv;";
            ofd.Title = "Select Altium Pick and Place file";
            ofd.RestoreDirectory = true;
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                openedFilePath = ofd.FileName;
                richTextBox1.Clear();
                dataList = new List<string[]>();
                // ファイル読み込み処理
                Stream stream;
                stream = ofd.OpenFile();
                if (stream != null)
                {
                    StreamReader sr = new StreamReader(stream);
                    while (!sr.EndOfStream)
                    {
                        // 1行読み出し
                        string colString = sr.ReadLine();
                        // CSVの書式の邪魔な部分を捨てる
                        dataList.Add(colString.Replace("\"", "").Split(','));
                    }
                    // 整形
                    dataList.RemoveAt(1);
                    dataList.RemoveAt(dataList.Count - 1);
                    sr.Close();
                    stream.Close();
                    richTextBox1.AppendText("Reading file completed.\n");

                    // DataGridViewの初期化 (CSVを2回読み込むと例外発生・・・)
                    dataGridView1.ColumnCount = 11;
                    for (int i = 0; i < 11; i++)
                    {
                        dataGridView1.Columns[i].HeaderText = dataList[0][i];
                    }
                    // 変な列が作成されるのを防ぐ
                    dataGridView1.AutoGenerateColumns = false;
                    dataGridView1.DataSource = dataList;

                    int insertRowIndex = dataGridView1.CurrentCell.RowIndex;
                    for (int i = 1; i < dataList.Count; i++)
                    {
                        DataGridViewRow row = dataGridView1.Rows[insertRowIndex];
                        for (int j = 0; j < row.Cells.Count; j++)
                        {
                            // 表面と裏面で色を変える
                            if (dataList[i][8] == "B")
                            {
                                row.Cells[j].Style.BackColor = Color.LightCyan;
                            }
                            else
                            {
                                row.Cells[j].Style.BackColor = Color.MistyRose;
                            }
                            row.Cells[j].Value = dataList[i][j];
                        }
                        insertRowIndex++;
                    }

                    // データの分割処理　表と裏に分ける
                    topPick = new List<PickPlace>();
                    bottomPick = new List<PickPlace>();
                    for (int i = 1; i < dataList.Count; i++)
                    {
                        if (dataList[i][8] == "B")
                        {
                            bottomPick.Add(new PickPlace(dataList[i]));
                        }
                        else
                        {
                            topPick.Add(new PickPlace(dataList[i]));
                        }
                    }
                }// データ読み込み，振り分け完了
            }

        }
        /// <summary>
        /// 単位変換(mil -> mm)
        /// </summary>
        /// <param name="mil"></param>
        /// <returns></returns>
        public static double mil2mm(double mil)
        {
            return mil * 25.4 / 1000;
        }
        /// <summary>
        /// 単位変換(mm -> mil)
        /// </summary>
        /// <param name="mm"></param>
        /// <returns></returns>
        public static double mm2mil(double mm)
        {
            return mm / 25.4 * 1000;
        }

        /// <summary>
        /// 配置位置情報
        /// </summary>
        class PickPlace
        {
            string Designator;
            string Footprint;
            double MidX;
            double MidY;
            double RefX;
            double RefY;
            double PadX;
            double PadY;
            char Layer;
            double Rotation;
            string Comment;
            public PickPlace(string[] rowData)
            {
                if (rowData.Length > 11) MessageBox.Show("Invalid Input data.");
                this.Designator = rowData[0];
                this.Footprint = rowData[1];
                this.MidX = this.ConvDistanceInfo(rowData[2]);
                this.MidY = this.ConvDistanceInfo(rowData[3]);
                this.RefX = this.ConvDistanceInfo(rowData[4]);
                this.RefY = this.ConvDistanceInfo(rowData[5]);
                this.PadX = this.ConvDistanceInfo(rowData[6]);
                this.PadY = this.ConvDistanceInfo(rowData[7]);
                this.Layer = rowData[8].ToCharArray()[0];
                this.Rotation = Convert.ToDouble(rowData[9]);
                this.Comment = rowData[10];
            }
            /// <summary>
            /// 文字列から距離情報に変換する
            /// </summary>
            /// <param name="col"></param>
            /// <returns></returns>
            private double ConvDistanceInfo(string col)
            {
                if (col.Contains("mil"))
                {
                    // 単位系はmil
                    return Convert.ToDouble(col.Replace("mil", ""));
                }
                else if (col.Contains("mm"))
                {
                    return mm2mil(Convert.ToDouble(col.Replace("mm", "")));
                }
                else
                {
                    return 0;
                }
            }
            /// <summary>
            /// 文字列変換(1行出力)
            /// </summary>
            /// <param name="mode">単位系(mil/mm)</param>
            /// <returns>出力文字列</returns>
            public string ToString(unit mode)
            {
                if (mode == unit.mil)
                {
                    StringBuilder sb = new StringBuilder();
                    return sb.Append(this.Designator).Append(",").Append(this.Footprint).Append(",")
                        .Append(this.MidX.ToString()).Append("mil,").Append(this.MidY.ToString()).Append("mil,")
                        .Append(this.RefX.ToString()).Append("mil,").Append(this.RefY.ToString()).Append("mil,")
                        .Append(this.PadX.ToString()).Append("mil,").Append(this.PadY.ToString()).Append("mil,")
                        .Append(this.Layer.ToString()).Append(",").Append(this.Rotation.ToString()).Append(",")
                        .Append(this.Comment).Append("\r\n").ToString();
                }
                else if (mode == unit.mm)
                {
                    // 単位を変換してから出力
                    StringBuilder sb = new StringBuilder();
                    return sb.Append(this.Designator).Append(",").Append(this.Footprint).Append(",")
                        .Append(mil2mm(this.MidX).ToString()).Append("mm,").Append(mil2mm(this.MidY).ToString()).Append("mm,")
                        .Append(mil2mm(this.RefX).ToString()).Append("mm,").Append(mil2mm(this.RefY).ToString()).Append("mm,")
                        .Append(mil2mm(this.PadX).ToString()).Append("mm,").Append(mil2mm(this.PadY).ToString()).Append("mm,")
                        .Append(this.Layer.ToString()).Append(",").Append(this.Rotation.ToString()).Append(",")
                        .Append(this.Comment).Append("\r\n").ToString();
                }
                else
                {
                    return "Invalid unit!";
                }
            }
            /// <summary>
            /// Bottom Layerの座標変換
            /// </summary>
            /// <param name="Width">基板のX</param>
            /// <param name="Height">基板のY</param>
            /// <param name="_unit">単位系</param>
            /// <param name="mode">反転の方向</param>
            public void ConvertCoordinate(double Width, double Height, unit _unit, int mode)
            {
                // 単位系をmilに統一してから計算
                if (_unit == unit.mm)
                {
                    Width = mm2mil(Width);
                    Height = mm2mil(Height);
                }
                // 要考察(オフセット，角度など)　反転
                switch (mode)
                {
                    case 0:
                        // 反転なし
                        break;
                    case 1:
                        // X方向反転
                        this.MidX = Width - this.MidX;
                        this.RefX = Width - this.RefX;
                        this.PadX = Width - this.PadX;
                        // Rotation
                        this.Rotation = (540 - this.Rotation) % 360;
                        break;
                    case 2:
                        // Y方向反転
                        this.MidY = Height - this.MidY;
                        this.RefY = Height - this.RefY;
                        this.PadY = Height - this.PadY;
                        // Rotation
                        this.Rotation = 360 - this.Rotation;
                        break;
                    case 3:
                        // XY両反転
                        this.MidX = Width - this.MidX;
                        this.RefX = Width - this.RefX;
                        this.PadX = Width - this.PadX;
                        this.MidY = Height - this.MidY;
                        this.RefY = Height - this.RefY;
                        this.PadY = Height - this.PadY;
                        // Rotation (X and Y)
                        this.Rotation = 360 - this.Rotation;
                        this.Rotation = (540 - this.Rotation) % 360;
                        break;
                    default:
                        break;
                }

            }
        }
        /// <summary>
        /// 単位系の定義(mil/mm)
        /// </summary>
        enum unit
        {
            mil = 0,
            mm
        }
        /// <summary>
        /// 終了
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Exit_ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }
        // 保存
        private void button_save_Click(object sender, EventArgs e)
        {
            SaveCSV();
        }

        private void SaveData_ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveCSV();
        }
        /// <summary>
        /// CSVファイルに書き出し　1回以上処理が行われないように修正
        /// </summary>
        public void SaveCSV()
        {
            richTextBox1.Clear();
            // モードの確認
            unit _unit = unit.mil;
            if (radioButton_mil.Checked)
            {
                _unit = unit.mil;
            }
            else if (radioButton_mm.Checked)
            {
                _unit = unit.mm;
            }
            else
            {
                // 起こり得ないが・・・
                MessageBox.Show("Select correct unit.");
            }
            if (topPick != null && bottomPick != null)
            {
                if (button_save.Text != "Exit")
                {
                    // 表はそのまま書き出す
                    string topFilepath = openedFilePath.Replace(".csv", "_top.csv");
                    StreamWriter sw = new StreamWriter(topFilepath);
                    // 改行コードを明示的に指定(デフォルトでCR+LFになっているので不要か)
                    // sw.NewLine = ("\r\n");
                    // ヘッダ情報書き込み
                    sw.WriteLine("Designator,Footprint,Mid X,Mid Y,Ref X,Ref Y,Pad X,Pad Y,Layer,Rotation,Comment");
                    sw.WriteLine("");   // 空行追加．(必要かは不明)
                    for (int i = 0; i < topPick.Count; i++)
                    {
                        sw.Write(topPick[i].ToString(_unit));
                    }
                    richTextBox1.AppendText("Top layer data saved to : " + topFilepath + "\n");
                    sw.Close();

                    // 裏のデータの書き出し
                    if (textBox_Width.Text != "" && textBox_Height.Text != "")
                    {
                        string bottomFilepath = openedFilePath.Replace(".csv", "_bottom.csv");
                        sw = new StreamWriter(bottomFilepath);
                        // ヘッダ情報書き込み
                        sw.WriteLine("Designator,Footprint,Mid X,Mid Y,Ref X,Ref Y,Pad X,Pad Y,Layer,Rotation,Comment");
                        sw.WriteLine("");
                        // 反転モードの指定
                        int mode = new int();
                        if (radioButton_x.Checked) mode = 1;
                        if (radioButton_y.Checked) mode = 2;
                        for (int i = 0; i < bottomPick.Count; i++)
                        {
                            // 反転，書き込み
                            bottomPick[i].ConvertCoordinate(Convert.ToDouble(textBox_Width.Text), Convert.ToDouble(textBox_Height.Text), _unit, mode);
                            sw.Write(bottomPick[i].ToString(_unit));
                        }
                        sw.Close();
                        richTextBox1.AppendText("Bottom layer data saved to :" + bottomFilepath + "\n");
                        // 終了フラグ代わり
                        button_save.Text = "Exit";
                        SaveData_ToolStripMenuItem.Enabled = false;
                    }
                    else
                    {
                        richTextBox1.Clear();
                        richTextBox1.AppendText("Input board size (Both X and Y)\n");
                    }
                }
                else
                {
                    // 2回目のボタン押し
                    Application.Exit();
                }
            }
            else
            {
                MessageBox.Show("Please load data first!");
            }

        }
    }
}