import { escapeHtml } from '../shell';
import {
  getAccountOrganization,
  getAccountOrganizationMembers,
  inviteAccountOrganizationMember,
  removeAccountOrganizationMember,
  updateAccountOrganizationMemberRoles,
  updateAccountOrganizationName,
} from '../api';
import type { ApiError, AccountOrganizationMemberResponse, AccountOrganizationResponse, UserInfo } from '../api';
import { registerAccountStateReset } from '../accountContext';
import { accountPermissions, hasAccountPermission } from '../permissions';

const ORGANIZATION_ROLES = ['Organization Administrator', 'Manager', 'Editor', 'Viewer'];

interface OrganizationPageState {
  organization: AccountOrganizationResponse | null;
  members: AccountOrganizationMemberResponse[];
  loading: boolean;
  loaded: boolean;
  error: string | null;
  inviteNotice: string | null;
}

const state: OrganizationPageState = {
  organization: null,
  members: [],
  loading: false,
  loaded: false,
  error: null,
  inviteNotice: null,
};
registerAccountStateReset(() => {
  Object.assign(state, {
    organization: null,
    members: [],
    loading: false,
    loaded: false,
    error: null,
    inviteNotice: null,
  });
});

function rerender(): void {
  window.dispatchEvent(new Event('pxa:rerender'));
}

async function loadOrganization(canReadMembers = true): Promise<void> {
  if (state.loading) return;
  state.loading = true;
  state.error = null;
  try {
    const [organization, members] = await Promise.all([
      getAccountOrganization(),
      canReadMembers ? getAccountOrganizationMembers() : Promise.resolve([]),
    ]);
    state.organization = organization;
    state.members = members ?? [];
  } catch (error) {
    state.error = (error as ApiError).message;
  } finally {
    state.loading = false;
    state.loaded = true;
    rerender();
  }
}

function roleCheckboxes(name: string, checked: readonly string[] = []): string {
  return ORGANIZATION_ROLES.map((role) => `
    <label class="account-checkbox">
      <input type="checkbox" name="${name}" value="${escapeHtml(role)}" ${checked.includes(role) ? 'checked' : ''}>
      ${escapeHtml(role)}
    </label>
  `).join('');
}

function memberRow(member: AccountOrganizationMemberResponse, canAssignRoles: boolean, canRemove: boolean): string {
  return `
    <tr>
      <td>${escapeHtml(member.displayName)}<br><small>${escapeHtml(member.email)}</small></td>
      <td>${escapeHtml(member.membershipStatus)}</td>
      <td>
        ${canAssignRoles ? `<form class="account-member-roles-form" data-user-id="${escapeHtml(member.userId)}">
          <div class="account-role-checkboxes">${roleCheckboxes('roles', member.roles)}</div>
          <button class="pxa-button pxa-button--secondary" type="submit">Save roles</button>
        </form>` : escapeHtml(member.roles.join(', ') || 'No role')}
      </td>
      <td>${canRemove ? `<button class="pxa-button pxa-button--secondary account-member-remove" type="button" data-user-id="${escapeHtml(member.userId)}">Remove</button>` : ''}</td>
    </tr>
  `;
}

export function organizationPage(user: UserInfo): string {
  const canManageOrganization = hasAccountPermission(user, accountPermissions.organizationManage);
  const canReadMembers = hasAccountPermission(user, accountPermissions.membersRead);
  const canInviteMembers = hasAccountPermission(user, accountPermissions.membersInvite);
  const canRemoveMembers = hasAccountPermission(user, accountPermissions.membersRemove);
  if (!state.loaded && !state.loading) loadOrganization(canReadMembers);

  if (!state.organization) {
    return `
      <header class="account-page-header"><div><p class="pxa-kicker">Customer workspace</p><h1>Organization</h1></div></header>
      <section class="account-section">
        <div>${state.error ? `<p role="alert">${escapeHtml(state.error)}</p>` : '<p>Loading your organization…</p>'}</div>
      </section>
    `;
  }

  return `
    <header class="account-page-header">
      <div>
        <p class="pxa-kicker">Customer workspace</p>
        <h1>Organization</h1>
        <p>Manage your organization profile and invite, remove, or reassign member roles.</p>
      </div>
    </header>
    <section class="account-profile-forms">
      ${canManageOrganization ? `<form class="account-form" id="organization-name-form">
        <h2>Organization name</h2>
        <label>Name<input name="name" value="${escapeHtml(state.organization.name)}" required minlength="2" maxlength="200"></label>
        <div class="account-form-error" role="alert" hidden></div>
        <button class="pxa-button pxa-button--primary" type="submit">Save name</button>
      </form>` : ''}
      ${canReadMembers ? `<div class="account-form">
        <h2>Members</h2>
        <table class="account-table">
          <thead><tr><th>Member</th><th>Status</th><th>Roles</th><th></th></tr></thead>
          <tbody>${state.members.map((member) => memberRow(member, canInviteMembers, canRemoveMembers)).join('') || '<tr><td colspan="4">No members yet.</td></tr>'}</tbody>
        </table>
      </div>` : ''}
      ${canInviteMembers ? `<form class="account-form" id="organization-invite-form">
        <h2>Invite a teammate</h2>
        ${state.inviteNotice ? `<p class="account-message account-message--info">${escapeHtml(state.inviteNotice)}</p>` : ''}
        <label>Full name<input name="displayName" required maxlength="200"></label>
        <label>Email<input name="email" type="email" required></label>
        <div class="account-role-checkboxes">${roleCheckboxes('roles')}</div>
        <div class="account-form-error" role="alert" hidden></div>
        <button class="pxa-button pxa-button--primary" type="submit">Send invitation</button>
      </form>` : ''}
    </section>
  `;
}

function selectedRoles(form: HTMLFormElement, name: string): string[] {
  return Array.from(form.querySelectorAll<HTMLInputElement>(`input[name="${name}"]:checked`)).map((input) => input.value);
}

function bindOrganizationForm(formId: string, handler: (form: HTMLFormElement) => Promise<void>): void {
  const form = document.querySelector<HTMLFormElement>(formId);
  form?.addEventListener('submit', async (event) => {
    event.preventDefault();
    const error = form.querySelector<HTMLElement>('.account-form-error')!;
    const button = form.querySelector<HTMLButtonElement>('button[type="submit"]')!;
    error.hidden = true;
    error.textContent = '';
    button.disabled = true;
    try {
      await handler(form);
    } catch (submitError) {
      error.textContent = (submitError as ApiError).message;
      error.hidden = false;
      button.disabled = false;
    }
  });
}

export function bindOrganizationEvents(): void {
  bindOrganizationForm('#organization-name-form', async (form) => {
    const name = new FormData(form).get('name');
    state.organization = await updateAccountOrganizationName(String(name ?? ''));
    rerender();
  });

  bindOrganizationForm('#organization-invite-form', async (form) => {
    const data = new FormData(form);
    const roles = selectedRoles(form, 'roles');
    if (roles.length === 0) throw new Error('Select at least one role.');
    await inviteAccountOrganizationMember(String(data.get('email') ?? ''), String(data.get('displayName') ?? ''), roles);
    state.inviteNotice = 'Invitation sent.';
    await loadOrganization();
  });

  document.querySelectorAll<HTMLFormElement>('.account-member-roles-form').forEach((form) => {
    form.addEventListener('submit', async (event) => {
      event.preventDefault();
      const roles = selectedRoles(form, 'roles');
      const userId = form.dataset.userId!;
      try {
        await updateAccountOrganizationMemberRoles(userId, roles);
        await loadOrganization();
      } catch (error) {
        state.error = (error as ApiError).message;
        rerender();
      }
    });
  });

  document.querySelectorAll<HTMLButtonElement>('.account-member-remove').forEach((button) => {
    button.addEventListener('click', async () => {
      const userId = button.dataset.userId!;
      button.disabled = true;
      try {
        await removeAccountOrganizationMember(userId);
        await loadOrganization();
      } catch (error) {
        state.error = (error as ApiError).message;
        button.disabled = false;
        rerender();
      }
    });
  });
}
