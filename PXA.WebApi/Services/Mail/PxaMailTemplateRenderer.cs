using System.Globalization;
using System.Net;
using Microsoft.Extensions.Options;
using PXA.Domain.Entities;

namespace PXA.WebApi.Services.Mail;

public sealed class PxaMailTemplateRenderer(IOptions<PxaMailOptions> options)
{
    private const string Invitation = "identity.invitation";
    private const string PasswordReset = "identity.password-reset";
    private const string PasswordChanged = "identity.password-changed";
    private const string EmailVerification = "identity.email-verification";
    private const string EmailChanged = "identity.email-changed";
    private const string RegistrationVerification = "identity.registration-verification";
    private const string Welcome = "identity.welcome";
    private const string NewLogin = "identity.new-login";
    private const string Lockout = "identity.lockout";
    private const string TrialExpiring = "identity.trial-expiring";
    private const string SubscriptionChanged = "subscription.changed";
    private const string LicenseChanged = "license.changed";
    private const string OrganizationSecurityChanged = "security.organization-changed";

    private static readonly IReadOnlyDictionary<string, LocalePack> Locales =
        new Dictionary<string, LocalePack>(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = English(),
            ["de"] = German(),
            ["fr"] = French(),
            ["es"] = Spanish(),
            ["it"] = Italian(),
            ["ar"] = Arabic(),
        };

    private readonly PxaMailOptions options = options.Value;

    public RenderedMail Render(
        MailOutboxMessage message,
        IReadOnlyDictionary<string, string> payload)
    {
        var locale = NormalizeLocale(message.Locale);
        var pack = Locales[locale];
        if (!pack.Templates.TryGetValue(message.TemplateKey, out var copy))
            throw new PxaPermanentMailException("Unknown transactional mail template.");

        var displayName = payload.GetValueOrDefault("displayName", pack.DefaultUser);
        var body = copy.Body.Replace(
            "{date}",
            FormatDate(payload.GetValueOrDefault("trialEndsAt", string.Empty), locale),
            StringComparison.Ordinal);
        var actionUrl = ResolveActionUrl(copy.ActionTarget, payload);
        var html = BuildHtml(pack, copy, displayName, body, actionUrl);
        var text = BuildText(pack, copy, displayName, body, actionUrl);
        return new RenderedMail(message.Id, message.RecipientEmail, copy.Subject, html, text);
    }

    internal static string NormalizeLocale(string? locale)
    {
        if (string.IsNullOrWhiteSpace(locale))
            return "en";
        var language = locale.Split(['-', '_'], 2)[0].ToLowerInvariant();
        return Locales.ContainsKey(language) ? language : "en";
    }

    private string ResolveActionUrl(
        ActionTarget target,
        IReadOnlyDictionary<string, string> payload)
    {
        var candidate = target switch
        {
            ActionTarget.Payload => payload.GetValueOrDefault("actionUrl", string.Empty),
            ActionTarget.Account => options.AccountBaseUrl,
            ActionTarget.Support => options.SupportUrl,
            ActionTarget.Designer => options.DesignerBaseUrl,
            _ => string.Empty,
        };
        return IsSafePublicUrl(candidate) ? candidate : string.Empty;
    }

    private string BuildHtml(
        LocalePack pack,
        TemplateCopy copy,
        string displayName,
        string body,
        string actionUrl)
    {
        var direction = pack.IsRightToLeft ? "rtl" : "ltr";
        var textAlign = pack.IsRightToLeft ? "right" : "left";
        var safeName = WebUtility.HtmlEncode(displayName);
        var safeBody = WebUtility.HtmlEncode(body);
        var safeSubject = WebUtility.HtmlEncode(copy.Subject);
        var safeCompanyUrl = WebUtility.HtmlEncode(options.CompanyBaseUrl);
        var safeAccountUrl = WebUtility.HtmlEncode(options.AccountBaseUrl);
        var safeDesignerUrl = WebUtility.HtmlEncode(options.DesignerBaseUrl);
        var safeSupportUrl = WebUtility.HtmlEncode(options.SupportUrl);
        var action = string.IsNullOrEmpty(actionUrl) || string.IsNullOrEmpty(copy.ActionLabel)
            ? string.Empty
            : $"""
               <p style="margin:24px 0">
                 <a href="{WebUtility.HtmlEncode(actionUrl)}" style="display:inline-block;padding:12px 18px;color:#fff;background:#216fbd;border-radius:6px;text-decoration:none;font-weight:700">{WebUtility.HtmlEncode(copy.ActionLabel)}</a>
               </p>
               """;
        return $"""
                <!doctype html>
                <html lang="{pack.Locale}" dir="{direction}">
                <head><meta charset="utf-8"><meta name="viewport" content="width=device-width"></head>
                <body style="margin:0;padding:0;color:#26364d;background:#f4f6f9;font-family:Arial,sans-serif;text-align:{textAlign}">
                  <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#f4f6f9">
                    <tr><td align="center" style="padding:32px 16px">
                      <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:640px;background:#fff;border:1px solid #d8e0e8">
                        <tr><td style="padding:24px 32px;background:#132033">
                          <a href="{safeCompanyUrl}" style="color:#fff;text-decoration:none;font-size:18px;font-weight:700">Power Dox Automation</a>
                        </td></tr>
                        <tr><td style="padding:32px">
                          <h1 style="margin:0 0 20px;font-size:24px;line-height:1.3">{safeSubject}</h1>
                          <p style="margin:0 0 16px">{WebUtility.HtmlEncode(pack.Greeting)} {safeName},</p>
                          <p style="margin:0;line-height:1.6">{safeBody}</p>
                          {action}
                        </td></tr>
                        <tr><td style="padding:20px 32px;color:#607086;background:#f8fafc;font-size:13px;line-height:1.6">
                          <p style="margin:0 0 8px">{WebUtility.HtmlEncode(pack.AutomatedNotice)}</p>
                          <p style="margin:0">
                            <a href="{safeAccountUrl}">{WebUtility.HtmlEncode(pack.AccountLabel)}</a> ·
                            <a href="{safeDesignerUrl}">{WebUtility.HtmlEncode(pack.DesignerLabel)}</a> ·
                            <a href="{safeSupportUrl}">{WebUtility.HtmlEncode(pack.SupportLabel)}</a>
                          </p>
                        </td></tr>
                      </table>
                    </td></tr>
                  </table>
                </body>
                </html>
                """;
    }

    private string BuildText(
        LocalePack pack,
        TemplateCopy copy,
        string displayName,
        string body,
        string actionUrl)
    {
        var action = string.IsNullOrEmpty(actionUrl) || string.IsNullOrEmpty(copy.ActionLabel)
            ? string.Empty
            : $"{Environment.NewLine}{copy.ActionLabel}: {actionUrl}";
        return $"""
                {copy.Subject}

                {pack.Greeting} {displayName},

                {body}

                {pack.AutomatedNotice}
                {pack.AccountLabel}: {options.AccountBaseUrl}
                {pack.DesignerLabel}: {options.DesignerBaseUrl}
                {pack.SupportLabel}: {options.SupportUrl}
                {action}
                """;
    }

    private static string FormatDate(string value, string locale)
    {
        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date))
            return value;
        return date.ToString("d", CultureInfo.GetCultureInfo(locale));
    }

    private static bool IsSafePublicUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
         string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));

    private static LocalePack English() => Pack(
        "en", false, "Hello", "PXA user",
        "This automated transactional message was sent by Power Dox Automation.",
        "Account", "Designer", "Support",
        T(Invitation, "Your Power Dox Automation invitation", "You have been invited to join a Power Dox Automation organization.", "Accept invitation", ActionTarget.Payload),
        T(PasswordReset, "Reset your Power Dox Automation password", "A password reset was requested for your account.", "Reset password", ActionTarget.Payload),
        T(PasswordChanged, "Your Power Dox Automation password changed", "Your password was changed. Contact Support immediately if this was not you.", "Contact Support", ActionTarget.Support),
        T(EmailVerification, "Verify your new Power Dox Automation email address", "Confirm the new email address for your account.", "Verify email address", ActionTarget.Payload),
        T(EmailChanged, "Your Power Dox Automation email address changed", "Your account email address was changed. Contact Support immediately if this was not expected.", "Contact Support", ActionTarget.Support),
        T(RegistrationVerification, "Verify your Power Dox Automation account", "Confirm your email address to activate your account and Trial.", "Verify account", ActionTarget.Payload),
        T(Welcome, "Welcome to Power Dox Automation", "Your account and Trial are ready.", "Open account", ActionTarget.Account),
        T(NewLogin, "New sign-in to your Power Dox Automation account", "Your account was just signed in to. Contact Support immediately if this was not you.", "Contact Support", ActionTarget.Support),
        T(Lockout, "Your Power Dox Automation account was locked", "Your account was temporarily locked after too many unsuccessful sign-in attempts.", "Contact Support", ActionTarget.Support),
        T(TrialExpiring, "Your Power Dox Automation Trial is ending soon", "Your Trial ends on {date}. Review your plan to keep access to your workspace.", "Review subscription", ActionTarget.Account),
        T(SubscriptionChanged, "Your Power Dox Automation subscription changed", "Your organization's subscription settings or lifecycle status changed.", "Open subscription", ActionTarget.Account),
        T(LicenseChanged, "Your Power Dox Automation license changed", "An offline license for your organization was issued, updated, revoked, or is nearing expiry.", "Open licenses", ActionTarget.Account),
        T(OrganizationSecurityChanged, "Security change in your Power Dox Automation organization", "A role, seat, service account, API key, or organization security setting changed.", "Review security", ActionTarget.Account));

    private static LocalePack German() => Pack(
        "de", false, "Hallo", "PXA-Benutzer",
        "Diese automatische Transaktions-E-Mail wurde von Power Dox Automation gesendet.",
        "Konto", "Designer", "Support",
        T(Invitation, "Ihre Einladung zu Power Dox Automation", "Sie wurden in eine Power Dox Automation-Organisation eingeladen.", "Einladung annehmen", ActionTarget.Payload),
        T(PasswordReset, "Power Dox Automation-Passwort zurücksetzen", "Für Ihr Konto wurde das Zurücksetzen des Passworts angefordert.", "Passwort zurücksetzen", ActionTarget.Payload),
        T(PasswordChanged, "Ihr Power Dox Automation-Passwort wurde geändert", "Ihr Passwort wurde geändert. Kontaktieren Sie sofort den Support, wenn Sie dies nicht waren.", "Support kontaktieren", ActionTarget.Support),
        T(EmailVerification, "Neue E-Mail-Adresse bestätigen", "Bestätigen Sie die neue E-Mail-Adresse für Ihr Power Dox Automation-Konto.", "E-Mail-Adresse bestätigen", ActionTarget.Payload),
        T(EmailChanged, "Ihre E-Mail-Adresse wurde geändert", "Die E-Mail-Adresse Ihres Kontos wurde geändert. Kontaktieren Sie bei einer unerwarteten Änderung sofort den Support.", "Support kontaktieren", ActionTarget.Support),
        T(RegistrationVerification, "Bestätigen Sie Ihr Power Dox Automation-Konto", "Bestätigen Sie Ihre E-Mail-Adresse, um Ihr Konto und Ihre Testphase zu aktivieren.", "Konto bestätigen", ActionTarget.Payload),
        T(Welcome, "Willkommen bei Power Dox Automation", "Ihr Konto und Ihre Testphase sind bereit.", "Konto öffnen", ActionTarget.Account),
        T(NewLogin, "Neue Anmeldung bei Ihrem Power Dox Automation-Konto", "Bei Ihrem Konto wurde eine neue Anmeldung erkannt. Kontaktieren Sie sofort den Support, wenn Sie dies nicht waren.", "Support kontaktieren", ActionTarget.Support),
        T(Lockout, "Ihr Power Dox Automation-Konto wurde gesperrt", "Ihr Konto wurde nach zu vielen fehlgeschlagenen Anmeldeversuchen vorübergehend gesperrt.", "Support kontaktieren", ActionTarget.Support),
        T(TrialExpiring, "Ihre Power Dox Automation-Testphase endet bald", "Ihre Testphase endet am {date}. Prüfen Sie Ihren Tarif, um den Zugriff zu behalten.", "Abonnement prüfen", ActionTarget.Account),
        T(SubscriptionChanged, "Ihr Power Dox Automation-Abonnement wurde geändert", "Einstellungen oder Status des Abonnements Ihrer Organisation wurden geändert.", "Abonnement öffnen", ActionTarget.Account),
        T(LicenseChanged, "Ihre Power Dox Automation-Lizenz wurde geändert", "Eine Offline-Lizenz Ihrer Organisation wurde ausgestellt, geändert, widerrufen oder läuft bald ab.", "Lizenzen öffnen", ActionTarget.Account),
        T(OrganizationSecurityChanged, "Sicherheitsänderung in Ihrer Organisation", "Eine Rolle, ein Benutzerplatz, ein Dienstkonto, ein API-Schlüssel oder eine Sicherheitseinstellung wurde geändert.", "Sicherheit prüfen", ActionTarget.Account));

    private static LocalePack French() => Pack(
        "fr", false, "Bonjour", "utilisateur PXA",
        "Ce message transactionnel automatique a été envoyé par Power Dox Automation.",
        "Compte", "Designer", "Assistance",
        T(Invitation, "Votre invitation Power Dox Automation", "Vous avez été invité à rejoindre une organisation Power Dox Automation.", "Accepter l'invitation", ActionTarget.Payload),
        T(PasswordReset, "Réinitialisez votre mot de passe Power Dox Automation", "Une réinitialisation du mot de passe a été demandée pour votre compte.", "Réinitialiser le mot de passe", ActionTarget.Payload),
        T(PasswordChanged, "Votre mot de passe Power Dox Automation a été modifié", "Votre mot de passe a été modifié. Contactez immédiatement l'assistance si vous n'êtes pas à l'origine de cette action.", "Contacter l'assistance", ActionTarget.Support),
        T(EmailVerification, "Vérifiez votre nouvelle adresse e-mail", "Confirmez la nouvelle adresse e-mail de votre compte Power Dox Automation.", "Vérifier l'adresse e-mail", ActionTarget.Payload),
        T(EmailChanged, "Votre adresse e-mail a été modifiée", "L'adresse e-mail de votre compte a été modifiée. Contactez immédiatement l'assistance si cette action est inattendue.", "Contacter l'assistance", ActionTarget.Support),
        T(RegistrationVerification, "Vérifiez votre compte Power Dox Automation", "Confirmez votre adresse e-mail pour activer votre compte et votre période d'essai.", "Vérifier le compte", ActionTarget.Payload),
        T(Welcome, "Bienvenue dans Power Dox Automation", "Votre compte et votre période d'essai sont prêts.", "Ouvrir le compte", ActionTarget.Account),
        T(NewLogin, "Nouvelle connexion à votre compte Power Dox Automation", "Une nouvelle connexion à votre compte a été détectée. Contactez immédiatement l'assistance si ce n'était pas vous.", "Contacter l'assistance", ActionTarget.Support),
        T(Lockout, "Votre compte Power Dox Automation a été verrouillé", "Votre compte a été temporairement verrouillé après trop de tentatives de connexion infructueuses.", "Contacter l'assistance", ActionTarget.Support),
        T(TrialExpiring, "Votre période d'essai Power Dox Automation se termine bientôt", "Votre période d'essai se termine le {date}. Vérifiez votre offre pour conserver l'accès.", "Voir l'abonnement", ActionTarget.Account),
        T(SubscriptionChanged, "Votre abonnement Power Dox Automation a été modifié", "Les paramètres ou l'état de l'abonnement de votre organisation ont été modifiés.", "Ouvrir l'abonnement", ActionTarget.Account),
        T(LicenseChanged, "Votre licence Power Dox Automation a été modifiée", "Une licence hors ligne de votre organisation a été émise, modifiée, révoquée ou arrive bientôt à expiration.", "Ouvrir les licences", ActionTarget.Account),
        T(OrganizationSecurityChanged, "Modification de sécurité dans votre organisation", "Un rôle, une licence utilisateur, un compte de service, une clé API ou un paramètre de sécurité a été modifié.", "Vérifier la sécurité", ActionTarget.Account));

    private static LocalePack Spanish() => Pack(
        "es", false, "Hola", "usuario de PXA",
        "Este mensaje transaccional automático fue enviado por Power Dox Automation.",
        "Cuenta", "Designer", "Soporte",
        T(Invitation, "Tu invitación a Power Dox Automation", "Has recibido una invitación para unirte a una organización de Power Dox Automation.", "Aceptar invitación", ActionTarget.Payload),
        T(PasswordReset, "Restablece tu contraseña de Power Dox Automation", "Se solicitó restablecer la contraseña de tu cuenta.", "Restablecer contraseña", ActionTarget.Payload),
        T(PasswordChanged, "Tu contraseña de Power Dox Automation cambió", "Tu contraseña fue modificada. Contacta con Soporte inmediatamente si no fuiste tú.", "Contactar con Soporte", ActionTarget.Support),
        T(EmailVerification, "Verifica tu nueva dirección de correo", "Confirma la nueva dirección de correo de tu cuenta de Power Dox Automation.", "Verificar correo", ActionTarget.Payload),
        T(EmailChanged, "Tu dirección de correo cambió", "La dirección de correo de tu cuenta fue modificada. Contacta con Soporte si no esperabas este cambio.", "Contactar con Soporte", ActionTarget.Support),
        T(RegistrationVerification, "Verifica tu cuenta de Power Dox Automation", "Confirma tu correo para activar la cuenta y el periodo de prueba.", "Verificar cuenta", ActionTarget.Payload),
        T(Welcome, "Te damos la bienvenida a Power Dox Automation", "Tu cuenta y tu periodo de prueba están listos.", "Abrir cuenta", ActionTarget.Account),
        T(NewLogin, "Nuevo inicio de sesión en tu cuenta", "Se detectó un nuevo inicio de sesión. Contacta con Soporte inmediatamente si no fuiste tú.", "Contactar con Soporte", ActionTarget.Support),
        T(Lockout, "Tu cuenta de Power Dox Automation fue bloqueada", "Tu cuenta fue bloqueada temporalmente después de demasiados intentos fallidos.", "Contactar con Soporte", ActionTarget.Support),
        T(TrialExpiring, "Tu periodo de prueba termina pronto", "Tu periodo de prueba termina el {date}. Revisa tu plan para conservar el acceso.", "Revisar suscripción", ActionTarget.Account),
        T(SubscriptionChanged, "Tu suscripción de Power Dox Automation cambió", "La configuración o el estado de la suscripción de tu organización cambió.", "Abrir suscripción", ActionTarget.Account),
        T(LicenseChanged, "Tu licencia de Power Dox Automation cambió", "Una licencia sin conexión de tu organización fue emitida, modificada, revocada o está próxima a vencer.", "Abrir licencias", ActionTarget.Account),
        T(OrganizationSecurityChanged, "Cambio de seguridad en tu organización", "Cambió un rol, un puesto, una cuenta de servicio, una clave API o una configuración de seguridad.", "Revisar seguridad", ActionTarget.Account));

    private static LocalePack Italian() => Pack(
        "it", false, "Ciao", "utente PXA",
        "Questo messaggio transazionale automatico è stato inviato da Power Dox Automation.",
        "Account", "Designer", "Assistenza",
        T(Invitation, "Il tuo invito a Power Dox Automation", "Sei stato invitato a entrare in un'organizzazione Power Dox Automation.", "Accetta l'invito", ActionTarget.Payload),
        T(PasswordReset, "Reimposta la password di Power Dox Automation", "È stata richiesta la reimpostazione della password del tuo account.", "Reimposta password", ActionTarget.Payload),
        T(PasswordChanged, "La password di Power Dox Automation è stata modificata", "La password è stata modificata. Contatta subito l'Assistenza se non sei stato tu.", "Contatta l'Assistenza", ActionTarget.Support),
        T(EmailVerification, "Verifica il nuovo indirizzo e-mail", "Conferma il nuovo indirizzo e-mail del tuo account Power Dox Automation.", "Verifica e-mail", ActionTarget.Payload),
        T(EmailChanged, "Il tuo indirizzo e-mail è stato modificato", "L'indirizzo e-mail dell'account è stato modificato. Contatta l'Assistenza se la modifica è inattesa.", "Contatta l'Assistenza", ActionTarget.Support),
        T(RegistrationVerification, "Verifica il tuo account Power Dox Automation", "Conferma l'indirizzo e-mail per attivare l'account e il periodo di prova.", "Verifica account", ActionTarget.Payload),
        T(Welcome, "Benvenuto in Power Dox Automation", "Il tuo account e il periodo di prova sono pronti.", "Apri account", ActionTarget.Account),
        T(NewLogin, "Nuovo accesso al tuo account", "È stato rilevato un nuovo accesso. Contatta subito l'Assistenza se non sei stato tu.", "Contatta l'Assistenza", ActionTarget.Support),
        T(Lockout, "Il tuo account Power Dox Automation è stato bloccato", "L'account è stato bloccato temporaneamente dopo troppi tentativi non riusciti.", "Contatta l'Assistenza", ActionTarget.Support),
        T(TrialExpiring, "Il periodo di prova termina a breve", "Il periodo di prova termina il {date}. Controlla il piano per mantenere l'accesso.", "Controlla abbonamento", ActionTarget.Account),
        T(SubscriptionChanged, "Il tuo abbonamento Power Dox Automation è stato modificato", "Le impostazioni o lo stato dell'abbonamento dell'organizzazione sono cambiati.", "Apri abbonamento", ActionTarget.Account),
        T(LicenseChanged, "La tua licenza Power Dox Automation è stata modificata", "Una licenza offline dell'organizzazione è stata emessa, modificata, revocata o sta per scadere.", "Apri licenze", ActionTarget.Account),
        T(OrganizationSecurityChanged, "Modifica di sicurezza nell'organizzazione", "Sono stati modificati un ruolo, una postazione, un account di servizio, una chiave API o un'impostazione di sicurezza.", "Controlla sicurezza", ActionTarget.Account));

    private static LocalePack Arabic() => Pack(
        "ar", true, "مرحبًا", "مستخدم PXA",
        "تم إرسال رسالة المعاملة التلقائية هذه بواسطة Power Dox Automation.",
        "الحساب", "المصمم", "الدعم",
        T(Invitation, "دعوتك إلى Power Dox Automation", "تمت دعوتك للانضمام إلى مؤسسة في Power Dox Automation.", "قبول الدعوة", ActionTarget.Payload),
        T(PasswordReset, "إعادة تعيين كلمة مرور Power Dox Automation", "تم طلب إعادة تعيين كلمة مرور حسابك.", "إعادة تعيين كلمة المرور", ActionTarget.Payload),
        T(PasswordChanged, "تم تغيير كلمة مرور Power Dox Automation", "تم تغيير كلمة مرورك. تواصل مع الدعم فورًا إذا لم تكن أنت من قام بذلك.", "التواصل مع الدعم", ActionTarget.Support),
        T(EmailVerification, "تأكيد عنوان بريدك الإلكتروني الجديد", "أكد عنوان البريد الإلكتروني الجديد لحساب Power Dox Automation.", "تأكيد البريد الإلكتروني", ActionTarget.Payload),
        T(EmailChanged, "تم تغيير عنوان بريدك الإلكتروني", "تم تغيير عنوان البريد الإلكتروني لحسابك. تواصل مع الدعم إذا لم يكن هذا التغيير متوقعًا.", "التواصل مع الدعم", ActionTarget.Support),
        T(RegistrationVerification, "تأكيد حساب Power Dox Automation", "أكد بريدك الإلكتروني لتفعيل حسابك والفترة التجريبية.", "تأكيد الحساب", ActionTarget.Payload),
        T(Welcome, "مرحبًا بك في Power Dox Automation", "حسابك والفترة التجريبية جاهزان.", "فتح الحساب", ActionTarget.Account),
        T(NewLogin, "تسجيل دخول جديد إلى حسابك", "تم اكتشاف تسجيل دخول جديد إلى حسابك. تواصل مع الدعم فورًا إذا لم تكن أنت.", "التواصل مع الدعم", ActionTarget.Support),
        T(Lockout, "تم قفل حساب Power Dox Automation", "تم قفل حسابك مؤقتًا بعد عدد كبير من محاولات تسجيل الدخول غير الناجحة.", "التواصل مع الدعم", ActionTarget.Support),
        T(TrialExpiring, "ستنتهي الفترة التجريبية قريبًا", "تنتهي الفترة التجريبية في {date}. راجع خطتك للحفاظ على الوصول.", "مراجعة الاشتراك", ActionTarget.Account),
        T(SubscriptionChanged, "تم تغيير اشتراك Power Dox Automation", "تم تغيير إعدادات اشتراك مؤسستك أو حالته.", "فتح الاشتراك", ActionTarget.Account),
        T(LicenseChanged, "تم تغيير ترخيص Power Dox Automation", "تم إصدار ترخيص غير متصل لمؤسستك أو تعديله أو إلغاؤه أو اقترب موعد انتهائه.", "فتح التراخيص", ActionTarget.Account),
        T(OrganizationSecurityChanged, "تغيير أمني في مؤسستك", "تم تغيير دور أو مقعد أو حساب خدمة أو مفتاح API أو إعداد أمني.", "مراجعة الأمان", ActionTarget.Account));

    private static LocalePack Pack(
        string locale,
        bool isRightToLeft,
        string greeting,
        string defaultUser,
        string automatedNotice,
        string accountLabel,
        string designerLabel,
        string supportLabel,
        params TemplateCopy[] templates) =>
        new(
            locale,
            isRightToLeft,
            greeting,
            defaultUser,
            automatedNotice,
            accountLabel,
            designerLabel,
            supportLabel,
            templates.ToDictionary(value => value.Key, StringComparer.Ordinal));

    private static TemplateCopy T(
        string key,
        string subject,
        string body,
        string actionLabel,
        ActionTarget actionTarget) =>
        new(key, subject, body, actionLabel, actionTarget);

    private sealed record LocalePack(
        string Locale,
        bool IsRightToLeft,
        string Greeting,
        string DefaultUser,
        string AutomatedNotice,
        string AccountLabel,
        string DesignerLabel,
        string SupportLabel,
        IReadOnlyDictionary<string, TemplateCopy> Templates);

    private sealed record TemplateCopy(
        string Key,
        string Subject,
        string Body,
        string ActionLabel,
        ActionTarget ActionTarget);

    private enum ActionTarget
    {
        Payload,
        Account,
        Support,
        Designer,
    }
}
