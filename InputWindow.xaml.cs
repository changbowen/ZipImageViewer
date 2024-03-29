﻿using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using static ZipImageViewer.Helpers;

namespace ZipImageViewer
{
    public partial class InputWindow : RoundedWindow
    {
        public class Field<T> where T : FrameworkElement
        {
            public string Name { get; protected set; }
            public T Control { get; protected set; }
            public Action<T> InitCallback { get; protected set; }
            public Field(string name = null, Action<T> init = null) {
                Name = name;
                InitCallback = init;

                var control = (T)Activator.CreateInstance(typeof(T));
                InitCallback?.Invoke(control);
                Control = control;
            }
        }

        public class Field<T, V> : Field<T> where T : FrameworkElement
        {
            public Func<T, V> ValueCallback { get; private set; }
            public V Value => ValueCallback == null ? default : ValueCallback(Control);

            public Field(string name = null, Action<T> init = null, Func<T, V> value = null) : base(name, init) {
                ValueCallback = value;
            }
        }


        public readonly KeyedCol<string, dynamic> Fields;
        public readonly Action<InputWindow> LoadedCallback;

        public InputWindow(KeyedCol<string, dynamic> fields, bool okOnly = false, Action<InputWindow> loaded = null) {
            InitializeComponent();
            if (okOnly) SP_OkCancel.Children.Remove(Btn_Cancel);

            Fields = fields;
            LoadedCallback = loaded;
        }

        public override void OnApplyTemplate() {
            foreach (var field in Fields) {
                if (field == null) continue;
                ContentPanel.Children.Add(field.Control);
            }

            base.OnApplyTemplate();
        }

        private void InputWin_Loaded(object sender, RoutedEventArgs e) {
            LoadedCallback?.Invoke(this);
        }

        private void Btn_OK_Click(object sender, RoutedEventArgs e) {
            DialogResult = new bool?(true);
        }


        public static (bool Answer, string Password, bool AddToFallback) PromptForArchivePassword() {
            var win = new InputWindow(new KeyedCol<string, dynamic>(f => f.Name) {
                new Field<TextBlock>(init: c => {
                    c.Text = GetRes("txt_PasswordForArchive");
                    c.Margin = new Thickness(0,0,0,10d);
                    c.FontSize = 16d;
                }),
                new Field<ContentControl, string>("PB_Password", init: c => {
                    c.Content = new PasswordBox();
                    c.Margin = new Thickness(0,0,0,10d);
                }, value: c => ((PasswordBox)c.Content).Password),
                new Field<CheckBox, bool?>("CB_Fallback", init: c => {
                    c.Content = GetRes("txt_AddToFallbackPwdLst");
                    c.HorizontalAlignment = HorizontalAlignment.Right;
                    c.Margin = new Thickness(0,0,0,10d);
                }, value: c => c.IsChecked),
                new Field<TextBlock>(init: c => c.Text = GetRes("msg_FallbackPwdTip")),
            }, loaded: w => FocusManager.SetFocusedElement(w.ContentPanel, w.Fields["PB_Password"].Control.Content));

            var result = (win.ShowDialog() == true, (string)win.Fields["PB_Password"].Value, (bool?)win.Fields["CB_Fallback"].Value == true);
            win.Fields.Clear();
            win.Close();
            return result;
        }

        public static (bool Answer, string CurrentPassword, string NewPassword, string ConfirmPassword)
            PromptForPasswordChange(bool showOldPassword = true, bool showIncorrectPassword = false, bool showMismatchPassword = false) {
            var fields = new KeyedCol<string, dynamic>(f => f.Name) {
                new Field<TextBlock>(init: c => c.Text = $"{GetRes("ttl_New_0", GetRes("ttl_Password"))}"),
                new Field<ContentControl, string>("NewPassword", init: c => {
                    c.Content = new PasswordBox();
                    c.Margin = new Thickness(0,5d,0,5d);
                }, value: c => ((PasswordBox)c.Content).Password),
                new Field<TextBlock>(init: c => c.Text = $"{GetRes("ttl_Confirm_0", GetRes("ttl_Password"))}"),
                new Field<ContentControl, string>("ConfirmPassword", init: c => {
                    c.Content = new PasswordBox();
                    c.Margin = new Thickness(0,5d,0,5d);
                }, value: c => ((PasswordBox)c.Content).Password),
            };
            if (showOldPassword) {
                fields.Insert(0, new Field<ContentControl, string>("CurrentPassword", init: c => {
                    c.Content = new PasswordBox();
                    c.Margin = new Thickness(0, 5d, 0, 5d);
                }, value: c => ((PasswordBox)c.Content).Password));
                fields.Insert(0, new Field<TextBlock>(init: c => c.Text = $"{GetRes("ttl_Current_0", GetRes("ttl_Password"))}"));
            }
            if (showIncorrectPassword) {
                fields.Insert(2, new Field<TextBlock>(init: c => {
                    c.Text = GetRes("msg_IncorrectPassword");
                    c.Foreground = Brushes.Red;
                    c.Margin = new Thickness(0, 0, 0, 5d);
                }));
            }
            if (showMismatchPassword) {
                fields.Add(new Field<TextBlock>(init: c => {
                    c.Text = GetRes("msg_MismatchPassword");
                    c.Foreground = Brushes.Red;
                    c.Margin = new Thickness(0, 0, 0, 5d);
                }));
            }
            var win = new InputWindow(fields, loaded: w => FocusManager.SetFocusedElement(w.ContentPanel, w.Fields[showOldPassword ? "CurrentPassword" : "NewPassword"].Control.Content)) {
                Title = $"{GetRes("ttl_Change_0", GetRes("ttl_MasterPassword"))}"
            };

            var result = (win.ShowDialog() == true,
                showOldPassword ? (string)win.Fields["CurrentPassword"].Value : null,
                (string)win.Fields["NewPassword"].Value,
                (string)win.Fields["ConfirmPassword"].Value);
            win.Fields.Clear();
            win.Close();
            return result;
        }

        public static (bool Answer, string MasterPassword) PromptForMasterPassword(bool showIncorrectPassword = false) {
            var win = new InputWindow(fields: new KeyedCol<string, dynamic>(f => f.Name) {
                new Field<TextBlock>(init: c => {
                    c.Text = GetRes("ttl_MasterPassword");
                    c.Margin = new Thickness(0,0,0,10d);
                }),
                new Field<ContentControl, string>("MasterPassword", init: c => {
                    c.Content = new PasswordBox();
                }, value: c => ((PasswordBox)c.Content).Password),
            }, loaded: w => FocusManager.SetFocusedElement(w.ContentPanel, w.Fields["MasterPassword"].Control.Content));
            if (showIncorrectPassword) {
                win.Fields.Add(new Field<TextBlock>(init: c => {
                    c.Text = GetRes("msg_IncorrectPassword");
                    c.Foreground = Brushes.Red;
                    c.Margin = new Thickness(0, 10d, 0, 0);
                }));
            }

            var result = (win.ShowDialog() == true, (string)win.Fields["MasterPassword"].Value);
            win.Fields.Clear();
            win.Close();
            return result;
        }

    }
}
