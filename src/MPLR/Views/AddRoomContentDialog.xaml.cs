using CommunityToolkit.Mvvm.ComponentModel;
using MPLR.Core;
using Wpf.Ui.Violeta.Controls;

namespace MPLR.Views;

[ObservableObject]
public sealed partial class AddRoomContentDialog : ContentDialog
{
    [ObservableProperty]
    private string? url = null;

    [ObservableProperty]
    private bool isForcedAdd = false;

    [ObservableProperty]
    private string? nickName = null;

    public string? RoomUrl = null;

    public ISpiderResult? SpiderResult { get; private set; }

    public AddRoomContentDialog()
    {
        DataContext = this;
        InitializeComponent();
    }

    private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Url))
        {
            Toast.Warning("EnterRoomUrl".Tr());
            e.Cancel = true;
            return;
        }

        if (IsForcedAdd)
        {
            string? roomUrl = Spider.ParseUrl(Url);

            if (roomUrl != null)
            {
                if (Configurations.Rooms.Get().Any(room => room.RoomUrl == roomUrl))
                {
                    e.Cancel = true;
                    Toast.Warning("AddRoomErrorDuplicated".Tr(roomUrl));
                    return;
                }

                NickName = roomUrl;
                RoomUrl = roomUrl;

                Toast.Success("AddRoomSucc".Tr(RoomUrl));
            }
            else
            {
                Toast.Error("ErrorRoomUrl".Tr());
            }
        }
        else
        {
            using (LoadingWindow.ShowAsync())
            {
                try
                {
                    ISpiderResult? spider = Spider.GetResult(Url);

                    if (string.IsNullOrWhiteSpace(spider?.Nickname))
                    {
                        e.Cancel = true;
                        Toast.Error(GetRoomInfoErrorMessage());
                        return;
                    }

                    if (Configurations.Rooms.Get().Any(room => room.RoomUrl == spider.RoomUrl))
                    {
                        e.Cancel = true;
                        Toast.Warning("AddRoomErrorDuplicated".Tr(spider.Nickname));
                        return;
                    }

                    NickName = spider.Nickname;
                    RoomUrl = spider.RoomUrl;
                    SpiderResult = spider;

                    Toast.Success("AddRoomSucc".Tr(NickName));
                }
                catch (Exception exception)
                {
                    e.Cancel = true;
                    Toast.Error(GetRoomInfoErrorMessage(exception.Message));
                }
            }
        }
    }

    private static string GetRoomInfoErrorMessage(string? fallback = null)
    {
        string error = ExternalStreamResolver.LastError;
        string message = string.IsNullOrWhiteSpace(error) ? fallback ?? string.Empty : error;

        if (IsCookieOrRiskError(message))
        {
            return "GetRoomInfoCookieError".Tr();
        }

        string genericMessage = "GetRoomInfoError".Tr();
        return string.IsNullOrWhiteSpace(message) ? genericMessage : $"{genericMessage}: {message}";
    }

    private static bool IsCookieOrRiskError(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        string value = message.ToLowerInvariant();
        string[] keywords =
        [
            "cookie",
            "login",
            "captcha",
            "forbidden",
            "blocked",
            "ip banned",
            "403",
            "401",
            "登录",
            "登陆",
            "验证码",
            "风控",
            "请求过快",
            "操作太快",
            "频繁",
        ];

        return keywords.Any(value.Contains);
    }
}

