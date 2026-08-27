import type { AccountLocale } from './authI18n';

export type DesignerAccessAction = 'subscription' | 'legalReview' | 'support' | 'dashboard' | 'retry';

export interface DesignerAccessErrorLike {
  status?: number;
  code?: string;
  message: string;
  isOffline?: boolean;
}

export interface DesignerAccessPresentation {
  kicker: string;
  title: string;
  message: string;
  primaryAction: DesignerAccessAction;
  secondaryAction?: DesignerAccessAction;
}

interface AccessCopy {
  restricted: string;
  trialTitle: string;
  trialMessage: string;
  entitlementTitle: string;
  entitlementMessage: string;
  permissionTitle: string;
  permissionMessage: string;
  organizationTitle: string;
  organizationMessage: string;
  verificationTitle: string;
  verificationMessage: string;
  accountTitle: string;
  accountMessage: string;
  legalTitle: string;
  legalMessage: string;
  sessionTitle: string;
  sessionMessage: string;
  offlineTitle: string;
  offlineMessage: string;
  invalidTitle: string;
  invalidMessage: string;
  unavailableTitle: string;
  unavailableMessage: string;
  actions: Record<DesignerAccessAction, string>;
}

const en: AccessCopy = {
  restricted: 'Designer access',
  trialTitle: 'Your Designer trial has expired',
  trialMessage: 'Your documents remain in your account, but opening PXA Designer requires an active subscription.',
  entitlementTitle: 'Designer is not included in your plan',
  entitlementMessage: 'The active organization does not currently have a Designer entitlement.',
  permissionTitle: 'Designer permission required',
  permissionMessage: 'Your organization role does not allow access to PXA Designer. Ask an organization administrator to update your role.',
  organizationTitle: 'Organization access unavailable',
  organizationMessage: 'Your organization or membership is not active. Contact your organization administrator or PXA Support.',
  verificationTitle: 'Verify your email address',
  verificationMessage: 'Email verification is required before PXA Designer can be opened.',
  accountTitle: 'Your account cannot open Designer',
  accountMessage: 'The account is disabled or temporarily locked. Review the account status or contact PXA Support.',
  legalTitle: 'Review the current legal documents',
  legalMessage: 'Complete the required legal review in PXA Account before opening Designer.',
  sessionTitle: 'Your Account session has expired',
  sessionMessage: 'Sign in to PXA Account again and then reopen Designer.',
  offlineTitle: 'PXA Designer is temporarily unreachable',
  offlineMessage: 'PXA Account cannot reach the API. Check the connection and try again.',
  invalidTitle: 'The Designer request is no longer valid',
  invalidMessage: 'The secure transfer may have expired. Start the Designer again to create a new request.',
  unavailableTitle: 'PXA Designer could not be opened',
  unavailableMessage: 'Access could not be verified. Try again or contact PXA Support with the diagnostic code.',
  actions: { subscription: 'View subscription', legalReview: 'Review legal documents', support: 'Open support', dashboard: 'Back to Account', retry: 'Try again' },
};

const copy: Record<AccountLocale, AccessCopy> = {
  en,
  de: {
    restricted: 'Designer-Zugriff', trialTitle: 'Ihr Designer-Testzugang ist abgelaufen',
    trialMessage: 'Ihre Dokumente bleiben in Ihrem Konto erhalten. Zum Öffnen von PXA Designer ist jedoch ein aktives Abonnement erforderlich.',
    entitlementTitle: 'Designer ist in Ihrem Tarif nicht enthalten', entitlementMessage: 'Die aktive Organisation besitzt derzeit keine Designer-Berechtigung.',
    permissionTitle: 'Designer-Berechtigung erforderlich', permissionMessage: 'Ihre Organisationsrolle erlaubt keinen Zugriff auf PXA Designer. Bitten Sie einen Organisationsadministrator, Ihre Rolle anzupassen.',
    organizationTitle: 'Organisationszugriff nicht verfügbar', organizationMessage: 'Ihre Organisation oder Mitgliedschaft ist nicht aktiv. Wenden Sie sich an Ihren Organisationsadministrator oder den PXA-Support.',
    verificationTitle: 'Bestätigen Sie Ihre E-Mail-Adresse', verificationMessage: 'Vor dem Öffnen von PXA Designer muss Ihre E-Mail-Adresse bestätigt werden.',
    accountTitle: 'Ihr Konto kann Designer nicht öffnen', accountMessage: 'Das Konto ist deaktiviert oder vorübergehend gesperrt. Prüfen Sie den Kontostatus oder wenden Sie sich an den PXA-Support.',
    legalTitle: 'Aktuelle Rechtstexte prüfen', legalMessage: 'Schließen Sie die erforderliche rechtliche Prüfung in PXA Account ab, bevor Sie Designer öffnen.',
    sessionTitle: 'Ihre Account-Sitzung ist abgelaufen', sessionMessage: 'Melden Sie sich erneut bei PXA Account an und öffnen Sie danach Designer.',
    offlineTitle: 'PXA Designer ist vorübergehend nicht erreichbar', offlineMessage: 'PXA Account kann die API nicht erreichen. Prüfen Sie die Verbindung und versuchen Sie es erneut.',
    invalidTitle: 'Die Designer-Anfrage ist nicht mehr gültig', invalidMessage: 'Die sichere Übertragung ist möglicherweise abgelaufen. Starten Sie Designer erneut, um eine neue Anfrage zu erstellen.',
    unavailableTitle: 'PXA Designer konnte nicht geöffnet werden', unavailableMessage: 'Der Zugriff konnte nicht geprüft werden. Versuchen Sie es erneut oder nennen Sie dem PXA-Support den Diagnosecode.',
    actions: { subscription: 'Abonnement ansehen', legalReview: 'Rechtstexte prüfen', support: 'Support öffnen', dashboard: 'Zurück zum Account', retry: 'Erneut versuchen' },
  },
  fr: {
    restricted: 'Accès au Designer', trialTitle: 'Votre essai Designer a expiré', trialMessage: 'Vos documents restent dans votre compte, mais un abonnement actif est nécessaire pour ouvrir PXA Designer.',
    entitlementTitle: 'Designer n’est pas inclus dans votre offre', entitlementMessage: 'L’organisation active ne dispose actuellement d’aucun droit Designer.',
    permissionTitle: 'Autorisation Designer requise', permissionMessage: 'Votre rôle ne permet pas d’accéder à PXA Designer. Demandez à un administrateur de modifier votre rôle.',
    organizationTitle: 'Accès à l’organisation indisponible', organizationMessage: 'Votre organisation ou votre adhésion n’est pas active. Contactez votre administrateur ou le support PXA.',
    verificationTitle: 'Vérifiez votre adresse e-mail', verificationMessage: 'La vérification de l’e-mail est requise avant d’ouvrir PXA Designer.',
    accountTitle: 'Votre compte ne peut pas ouvrir Designer', accountMessage: 'Le compte est désactivé ou temporairement verrouillé. Vérifiez son état ou contactez le support PXA.',
    legalTitle: 'Consultez les documents juridiques actuels', legalMessage: 'Terminez la vérification juridique requise dans PXA Account avant d’ouvrir Designer.',
    sessionTitle: 'Votre session Account a expiré', sessionMessage: 'Reconnectez-vous à PXA Account, puis rouvrez Designer.',
    offlineTitle: 'PXA Designer est temporairement inaccessible', offlineMessage: 'PXA Account ne peut pas joindre l’API. Vérifiez la connexion et réessayez.',
    invalidTitle: 'La demande Designer n’est plus valide', invalidMessage: 'Le transfert sécurisé a peut-être expiré. Redémarrez Designer pour créer une nouvelle demande.',
    unavailableTitle: 'Impossible d’ouvrir PXA Designer', unavailableMessage: 'L’accès n’a pas pu être vérifié. Réessayez ou communiquez le code de diagnostic au support PXA.',
    actions: { subscription: 'Voir l’abonnement', legalReview: 'Consulter les documents juridiques', support: 'Ouvrir le support', dashboard: 'Retour au compte', retry: 'Réessayer' },
  },
  es: {
    restricted: 'Acceso a Designer', trialTitle: 'Tu prueba de Designer ha caducado', trialMessage: 'Tus documentos permanecen en tu cuenta, pero necesitas una suscripción activa para abrir PXA Designer.',
    entitlementTitle: 'Designer no está incluido en tu plan', entitlementMessage: 'La organización activa no dispone actualmente del derecho de Designer.',
    permissionTitle: 'Se requiere permiso de Designer', permissionMessage: 'Tu rol no permite acceder a PXA Designer. Pide a un administrador que actualice tu rol.',
    organizationTitle: 'Acceso a la organización no disponible', organizationMessage: 'Tu organización o membresía no está activa. Contacta con el administrador o con soporte de PXA.',
    verificationTitle: 'Verifica tu correo electrónico', verificationMessage: 'Debes verificar tu correo antes de abrir PXA Designer.',
    accountTitle: 'Tu cuenta no puede abrir Designer', accountMessage: 'La cuenta está desactivada o bloqueada temporalmente. Revisa su estado o contacta con soporte de PXA.',
    legalTitle: 'Revisa los documentos legales actuales', legalMessage: 'Completa la revisión legal requerida en PXA Account antes de abrir Designer.',
    sessionTitle: 'Tu sesión de Account ha caducado', sessionMessage: 'Inicia sesión de nuevo en PXA Account y vuelve a abrir Designer.',
    offlineTitle: 'PXA Designer no está disponible temporalmente', offlineMessage: 'PXA Account no puede conectarse a la API. Comprueba la conexión e inténtalo de nuevo.',
    invalidTitle: 'La solicitud de Designer ya no es válida', invalidMessage: 'La transferencia segura puede haber caducado. Inicia Designer de nuevo para crear otra solicitud.',
    unavailableTitle: 'No se pudo abrir PXA Designer', unavailableMessage: 'No se pudo verificar el acceso. Inténtalo de nuevo o facilita el código de diagnóstico al soporte de PXA.',
    actions: { subscription: 'Ver suscripción', legalReview: 'Revisar documentos legales', support: 'Abrir soporte', dashboard: 'Volver a la cuenta', retry: 'Intentar de nuevo' },
  },
  it: {
    restricted: 'Accesso a Designer', trialTitle: 'La prova di Designer è scaduta', trialMessage: 'I documenti restano nel tuo account, ma per aprire PXA Designer è necessario un abbonamento attivo.',
    entitlementTitle: 'Designer non è incluso nel piano', entitlementMessage: 'L’organizzazione attiva non dispone attualmente dell’abilitazione Designer.',
    permissionTitle: 'Autorizzazione Designer richiesta', permissionMessage: 'Il tuo ruolo non consente l’accesso a PXA Designer. Chiedi a un amministratore di aggiornare il ruolo.',
    organizationTitle: 'Accesso all’organizzazione non disponibile', organizationMessage: 'L’organizzazione o l’appartenenza non è attiva. Contatta l’amministratore o il supporto PXA.',
    verificationTitle: 'Verifica il tuo indirizzo e-mail', verificationMessage: 'Prima di aprire PXA Designer è necessario verificare l’e-mail.',
    accountTitle: 'Il tuo account non può aprire Designer', accountMessage: 'L’account è disabilitato o temporaneamente bloccato. Controllane lo stato o contatta il supporto PXA.',
    legalTitle: 'Consulta i documenti legali correnti', legalMessage: 'Completa la revisione legale richiesta in PXA Account prima di aprire Designer.',
    sessionTitle: 'La sessione Account è scaduta', sessionMessage: 'Accedi nuovamente a PXA Account e poi riapri Designer.',
    offlineTitle: 'PXA Designer è temporaneamente irraggiungibile', offlineMessage: 'PXA Account non riesce a raggiungere l’API. Controlla la connessione e riprova.',
    invalidTitle: 'La richiesta Designer non è più valida', invalidMessage: 'Il trasferimento sicuro potrebbe essere scaduto. Avvia di nuovo Designer per creare una nuova richiesta.',
    unavailableTitle: 'Impossibile aprire PXA Designer', unavailableMessage: 'Non è stato possibile verificare l’accesso. Riprova o comunica il codice diagnostico al supporto PXA.',
    actions: { subscription: 'Visualizza abbonamento', legalReview: 'Consulta i documenti legali', support: 'Apri supporto', dashboard: 'Torna all’account', retry: 'Riprova' },
  },
  ar: {
    restricted: 'الوصول إلى المصمم', trialTitle: 'انتهت الفترة التجريبية للمصمم', trialMessage: 'ستبقى مستنداتك في حسابك، ولكن يلزم اشتراك نشط لفتح مصمم PXA.',
    entitlementTitle: 'المصمم غير مشمول في خطتك', entitlementMessage: 'لا تملك المؤسسة النشطة حالياً استحقاق استخدام المصمم.',
    permissionTitle: 'يلزم إذن لاستخدام المصمم', permissionMessage: 'لا يسمح دورك في المؤسسة بالوصول إلى مصمم PXA. اطلب من مسؤول المؤسسة تحديث دورك.',
    organizationTitle: 'الوصول إلى المؤسسة غير متاح', organizationMessage: 'مؤسستك أو عضويتك غير نشطة. تواصل مع مسؤول المؤسسة أو دعم PXA.',
    verificationTitle: 'تحقق من عنوان بريدك الإلكتروني', verificationMessage: 'يجب التحقق من البريد الإلكتروني قبل فتح مصمم PXA.',
    accountTitle: 'لا يمكن لحسابك فتح المصمم', accountMessage: 'الحساب معطل أو مقفل مؤقتاً. راجع حالة الحساب أو تواصل مع دعم PXA.',
    legalTitle: 'راجع المستندات القانونية الحالية', legalMessage: 'أكمل المراجعة القانونية المطلوبة في حساب PXA قبل فتح المصمم.',
    sessionTitle: 'انتهت جلسة حسابك', sessionMessage: 'سجّل الدخول إلى حساب PXA مرة أخرى ثم أعد فتح المصمم.',
    offlineTitle: 'مصمم PXA غير متاح مؤقتاً', offlineMessage: 'يتعذر على حساب PXA الوصول إلى الواجهة البرمجية. تحقق من الاتصال وحاول مجدداً.',
    invalidTitle: 'طلب المصمم لم يعد صالحاً', invalidMessage: 'ربما انتهت صلاحية النقل الآمن. افتح المصمم من جديد لإنشاء طلب جديد.',
    unavailableTitle: 'تعذر فتح مصمم PXA', unavailableMessage: 'تعذر التحقق من الوصول. حاول مجدداً أو زوّد دعم PXA برمز التشخيص.',
    actions: { subscription: 'عرض الاشتراك', legalReview: 'مراجعة المستندات القانونية', support: 'فتح الدعم', dashboard: 'العودة إلى الحساب', retry: 'المحاولة مجدداً' },
  },
};

const subscriptionCodes = new Set([
  'PXA_SUBSCRIPTION_CANCELLED', 'PXA_SUBSCRIPTION_INACTIVE', 'PXA_TRIAL_EXPIRED',
  'PXA_GRACE_PERIOD_EXPIRED', 'PXA_ENTITLEMENT_EXPIRED',
]);

export function describeDesignerAccessError(
  error: DesignerAccessErrorLike,
  locale: AccountLocale,
): DesignerAccessPresentation {
  const value = copy[locale] ?? en;
  if (error.isOffline)
    return { kicker: value.restricted, title: value.offlineTitle, message: value.offlineMessage, primaryAction: 'retry', secondaryAction: 'dashboard' };
  if (error.code && subscriptionCodes.has(error.code))
    return { kicker: value.restricted, title: value.trialTitle, message: value.trialMessage, primaryAction: 'subscription', secondaryAction: 'dashboard' };

  switch (error.code) {
    case 'PXA_ENTITLEMENT_MISSING':
    case 'PXA_ENTITLEMENT_DENIED':
      return { kicker: value.restricted, title: value.entitlementTitle, message: value.entitlementMessage, primaryAction: 'subscription', secondaryAction: 'dashboard' };
    case 'PXA_DESIGNER_PERMISSION_REQUIRED':
      return { kicker: value.restricted, title: value.permissionTitle, message: value.permissionMessage, primaryAction: 'dashboard', secondaryAction: 'support' };
    case 'PXA_DESIGNER_MEMBERSHIP_INACTIVE':
    case 'PXA_ORGANIZATION_INACTIVE':
    case 'PXAAPI016':
      return { kicker: value.restricted, title: value.organizationTitle, message: value.organizationMessage, primaryAction: 'dashboard', secondaryAction: 'support' };
    case 'PXA_DESIGNER_VERIFICATION_REQUIRED':
    case 'PXAAPI010':
      return { kicker: value.restricted, title: value.verificationTitle, message: value.verificationMessage, primaryAction: 'dashboard', secondaryAction: 'support' };
    case 'PXA_DESIGNER_ACCOUNT_DISABLED':
    case 'PXA_DESIGNER_ACCOUNT_LOCKED':
    case 'PXAAPI015':
      return { kicker: value.restricted, title: value.accountTitle, message: value.accountMessage, primaryAction: 'support', secondaryAction: 'dashboard' };
    case 'PXAAPI017':
      return { kicker: value.restricted, title: value.legalTitle, message: value.legalMessage, primaryAction: 'legalReview', secondaryAction: 'dashboard' };
    case 'PXA_DESIGNER_SESSION_EXPIRED':
      return { kicker: value.restricted, title: value.sessionTitle, message: value.sessionMessage, primaryAction: 'retry', secondaryAction: 'dashboard' };
    case 'PXA_DESIGNER_HANDOFF_INVALID':
      return { kicker: value.restricted, title: value.invalidTitle, message: value.invalidMessage, primaryAction: 'retry', secondaryAction: 'dashboard' };
    default:
      return { kicker: value.restricted, title: value.unavailableTitle, message: value.unavailableMessage, primaryAction: error.status === 400 ? 'retry' : 'dashboard', secondaryAction: 'support' };
  }
}

export function designerAccessActionLabel(action: DesignerAccessAction, locale: AccountLocale): string {
  return (copy[locale] ?? en).actions[action];
}
