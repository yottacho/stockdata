﻿using stockdata.forms.data;
using stockdata.forms.manager;
using stockdata.forms.report;
using stockdata.utils;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Windows.Forms;

namespace stockdata
{
    public partial class frmMainWindow : Form
    {
        public frmMainWindow()
        {
            InitializeComponent();

            // 타이틀에 버전명 추가
            this.Text += "- " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();

            // 화면 사이즈 조정
            if (Configure.windowX >= 0 && Configure.windowY >= 0)
            {
                this.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
                this.Location = new System.Drawing.Point(Configure.windowX, Configure.windowY);
            }

            if (Configure.windowW >= 0 && Configure.windowH >= 0)
                this.ClientSize = new System.Drawing.Size(Configure.windowW, Configure.windowH);

            // 상태바 업데이트
            toolStripStatusLabel1.Text = "접속서버: " + Configure.server + "/" + Configure.apiVersion;

            // 업데이트 체크
            Application.Idle += Check_Version;
        }

        private bool hiddenUpdateMessage = false;
        private void Check_Version(object sender, EventArgs e)
        {
            Application.Idle -= Check_Version;
            
            string curdate = DateTime.Now.ToString("yyyyMMdd");
            if (!curdate.Equals(Configure.lastUpdateCheck))
            {
                hiddenUpdateMessage = true;
                checkNewVersion_ToolStripMenuItem_Click(sender, e);
                Configure.lastUpdateCheck = curdate;
            }
        }

        /// <summary>
        /// Drag-n-drop 지원. 파일만 드래그할 수 있다.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void frmMain_DragEnter(object sender, DragEventArgs e)
        {
            // 사용 가능한 데이터 포맷 결정
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
            else
                e.Effect = DragDropEffects.None;
        }

        /// <summary>
        /// 파일을 드래그하면 자료입력 창을 실행한다
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void frmMain_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (string filePath in files)
                {
                    Console.WriteLine(filePath);

                    frmLoadData frmL = new frmLoadData(filePath);
                    frmL.MdiParent = this;
                    frmL.Show();
                }
            }
        }

        /// <summary>
        /// 프로그램 종료 메뉴
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void exit_ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (System.Windows.Forms.Application.MessageLoop)
            {
                // WinForms app
                System.Windows.Forms.Application.Exit();
            }
            else
            {
                // Console app
                System.Environment.Exit(1);
            }
        }

        /// <summary>
        /// 자료 파일을 불러오는 창을 연다.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void registerFile_ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            frmLoadData frmL = new frmLoadData();
            frmL.MdiParent = this;
            frmL.Show();
        }

        /// <summary>
        /// 새 버전 체크
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void checkNewVersion_ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            HttpRestClient client = new HttpRestClient("clientversion");
            if (!client.doWorkDialog())
            {
                client.showErrorDialog();
                return;
            }

            dynamic json = client.getJsonObject();
            if (json == null)
            {
                MessageBox.Show("전송 오류가 발생했습니다. (JSON 포맷오류)", "Format error!");
                return;
            }
            Console.WriteLine("statusCode: " + json["_metadata"]["statusCode"]);
            Console.WriteLine("version [" + json["clientversion"] + "]");
            Console.WriteLine("setup [" + json["clientsetup"] + "]");
            Console.WriteLine("md5 [" + json["clientmd5"] + "]");

            Version current = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            Version future = new Version((string)json["clientversion"]);

            if (current >= future)
            {
                if (!hiddenUpdateMessage)
                    MessageBox.Show("최신 버전을 사용하고 있습니다.", "버전 확인");
                hiddenUpdateMessage = false;
                return;
            }
            else
            {
                MessageBox.Show("새 버전이 있습니다. [" + future.ToString() + "]", "버전 확인");

                // 새 버전 자동 다운로드
                client = new HttpRestClient();
                client.RequestUri = (string)json["clientsetup"];
                if (!client.doWorkDialog())
                {
                    MessageBox.Show("다운로드 오류: " + client.ResponseMessage, "Request error!");
                    return;
                }

                string md5value = "";
                using (MD5 md5sum = MD5.Create())
                {
                    byte[] hashValue = md5sum.ComputeHash(client.ResponseContent);
                    for (int i = 0; i < hashValue.Length; i++)
                    {
                        md5value += hashValue[i].ToString("x2");
                    }
                }
                if (!md5value.Equals((string)json["clientmd5"]))
                {
                    MessageBox.Show("다운로드 파일 오류입니다. (md5 불일치)", "버전 확인");
                    return;
                }

                // 다운로드한 파일을 저장
                string localFileName = AppDomain.CurrentDomain.BaseDirectory + System.IO.Path.DirectorySeparatorChar + "Setup.exe";
                using (FileStream fs = File.Create(localFileName))
                {
                    fs.Write(client.ResponseContent, 0, client.ResponseContent.Length);
                }

                // 저장한 파일을 실행
                System.Diagnostics.Process.Start(localFileName);

                // 현재 프로그램을 종료
                exit_ToolStripMenuItem_Click(sender, e);
            }
        }

        /// <summary>
        /// 폼이 종료될 때 실행, 폼의 현재위치 및 크기를 저장
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void frmMainWindow_FormClosed(object sender, FormClosedEventArgs e)
        {
            // 현재 화면 위치 및 크기 저장
            Configure.windowX = this.Location.X;
            Configure.windowY = this.Location.Y;
            Configure.windowW = this.ClientSize.Width;
            Configure.windowH = this.ClientSize.Height;
        }

        /// <summary>
        /// 환경설정 창 열기
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void settingsOpen_ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            frmSettings frmL = new frmSettings();
            frmL.MdiParent = this;
            frmL.Show();
        }

        /// <summary>
        /// 서버 자료 조회 창 열기
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void viewServerData_ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            frmViewData frmL = new frmViewData();
            frmL.MdiParent = this;
            frmL.Show();
        }

        /// <summary>
        ///  보고서 집계표 창 열기
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void invfgnRpt_ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            frm집계표 frmL = new frm집계표();
            frmL.MdiParent = this;
            frmL.Show();
        }

        /// <summary>
        /// 종목변동현황 창 열기
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void stDiffRpt_ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            frm종목변동현황 frmL = new frm종목변동현황();
            frmL.MdiParent = this;
            frmL.Show();
        }

        /// <summary>
        /// API 로그 관리 창 열기
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void apilogToolStripMenuItem_Click(object sender, EventArgs e)
        {
            frmLogManage frmL = new frmLogManage();
            frmL.MdiParent = this;
            frmL.Show();
        }

        /// <summary>
        /// 오류로그 관리 창 열기
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void errorlogToolStripMenuItem_Click(object sender, EventArgs e)
        {
            frmErrorManage frmL = new frmErrorManage();
            frmL.MdiParent = this;
            frmL.Show();
        }

        /// <summary>
        /// 시총대비 순매수비율 창 열기
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void 시총대비순매수비율ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            frm기관시총대비순매수ONL frmL = new frm기관시총대비순매수ONL();
            frmL.MdiParent = this;
            frmL.Show();
        }
    }
}
