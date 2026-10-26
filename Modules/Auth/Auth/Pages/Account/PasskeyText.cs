namespace Auth.Pages.Account;

/// <summary>Every string on the passkey sign-in page, per language. The header and language switcher are <see cref="LoginText"/>'s.</summary>
public sealed record PasskeyText(
    string Heading,
    string Status,
    string Verifying,
    string Retry,
    string UsePassword,
    string FailedError,
    string CancelledMessage,
    string RetryPrompt,
    string UnsupportedMessage)
{
    internal static readonly Dictionary<string, PasskeyText> Texts = new()
    {
        ["en"] = new PasskeyText(
            Heading: "Verify with your passkey",
            Status: "Follow the prompt on your device — scan your fingerprint or face, or enter your device PIN.",
            Verifying: "Checking your passkey...",
            Retry: "Try again",
            UsePassword: "Use a password instead",
            FailedError: "Passkey sign-in did not work. Try again, or use a password instead.",
            CancelledMessage: "Cancelled or timed out. Press Try again when you are ready.",
            RetryPrompt: "Press Try again when you are ready.",
            UnsupportedMessage: "This browser does not support passkeys. Use a password instead."),
        ["th"] = new PasskeyText(
            Heading: "ยืนยันตัวตนด้วย Passkey",
            Status: "ทำตามหน้าต่างที่เครื่องแสดงขึ้นมา — สแกนลายนิ้วมือ ใบหน้า หรือใส่ PIN ของเครื่อง",
            Verifying: "กำลังตรวจสอบ Passkey...",
            Retry: "ลองอีกครั้ง",
            UsePassword: "ใช้รหัสผ่านแทน",
            FailedError: "ยืนยันตัวตนด้วย Passkey ไม่สำเร็จ กรุณาลองอีกครั้ง หรือใช้รหัสผ่านแทน",
            CancelledMessage: "ยกเลิกหรือหมดเวลา กด “ลองอีกครั้ง” เมื่อพร้อม",
            RetryPrompt: "กด “ลองอีกครั้ง” เมื่อพร้อม",
            UnsupportedMessage: "เบราว์เซอร์นี้ไม่รองรับ Passkey กรุณาใช้รหัสผ่านแทน"),
        ["zh"] = new PasskeyText(
            Heading: "使用通行密钥验证身份",
            Status: "请按设备上弹出的提示操作——扫描指纹或面部，或输入设备 PIN。",
            Verifying: "正在验证通行密钥...",
            Retry: "重试",
            UsePassword: "改用密码登录",
            FailedError: "通行密钥登录未成功。请重试，或改用密码登录。",
            CancelledMessage: "已取消或超时。准备好后请点击“重试”。",
            RetryPrompt: "准备好后请点击“重试”。",
            UnsupportedMessage: "此浏览器不支持通行密钥，请改用密码登录。"),
    };

    public static PasskeyText For(string lang) => Texts[lang];
}
