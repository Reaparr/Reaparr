import { route } from '@fixtures';
import { AuthenticationPaths } from '@api-urls';
import { generateFailedResultDTO, generateResultDTO } from '@mock';
import type { AppCredentialsDTO } from '@dto';

function installCredentialResponses() {
	let credentials: AppCredentialsDTO = {
		userName: 'ExistingUser22', password: 'masked-password', isDefaultCredentials: false,
	};
	let saveAttempts = 0;
	let credentialLoads = 0;
	let heldLoads = 0;
	const releases: Array<() => void> = [];
	cy.intercept('GET', '**/api/Integration', { statusCode: 200, body: generateResultDTO([]) });
	cy.intercept('GET', AuthenticationPaths.getAppCredentials(), (request) => {
		credentialLoads++;
		if (credentialLoads <= 2) {
			request.on('before:response', () => {
				heldLoads++;
				return new Promise<void>((resolve) => {
					releases.push(resolve);
				});
			});
		}
		request.reply({ statusCode: 200, body: generateResultDTO(credentials) });
	}).as('getCredentials');
	cy.intercept('PUT', AuthenticationPaths.updateCredentialsEndpoint(), (request) => {
		saveAttempts++;
		expect(request.body.password).to.equal(null);
		if (saveAttempts === 1) {
			expect(request.body.username).to.equal('Invalid Username');
			request.reply({
				statusCode: 400,
				body: generateFailedResultDTO({
					statusCode: 400,
					errors: [{ message: 'Username contains an invalid character', reasons: [], metadata: {} }],
				}),
			});
			return;
		}
		expect(request.body.username).to.equal('RecoveredUser22');
		credentials = { ...credentials, userName: 'RecoveredUser22' };
		request.reply({ statusCode: 200, body: generateResultDTO(null) });
	}).as('saveCredentials');
	return {
		get saveAttempts() { return saveAttempts; },
		get heldLoads() { return heldLoads; },
		release: () => releases.splice(0).forEach((resolve) => resolve()),
	};
}

describe('App credential save recovery', () => {
	let releasePending: (() => void) | undefined;
	afterEach(() => {
		releasePending?.();
		releasePending = undefined;
	});

	it('Should lock pending credentials and keep a rejected username editable until a corrected save', () => {
		cy.basePageSetup({ isLoggedIn: true, plexAccountCount: 0, plexServerCount: 0 }).then(() => {
			const backend = installCredentialResponses();
			releasePending = backend.release;
			cy.visit(route('/settings/accounts'));
			cy.wrap(null).should(() => expect(backend.heldLoads).to.equal(1));
			cy.getCy('app-username-input').should('be.disabled');
			cy.getCy('app-password-input').should('be.disabled');
			cy.getCy('app-confirm-password-input').should('be.disabled');
			cy.getCy('save-credentials-button').should('be.disabled').click({ force: true });
			cy.then(() => expect(backend.saveAttempts).to.equal(0));
			cy.then(() => backend.release());
			cy.wait('@getCredentials');
			cy.getCy('app-username-input').should('have.value', 'ExistingUser22').clear().type('Invalid Username');
			cy.getCy('save-credentials-button').should('not.be.disabled').click();
			cy.wait('@saveCredentials').its('response.statusCode').should('equal', 400);
			cy.getCy('alert-dialog').should('be.visible');
			cy.getCy('api-error').should('contain.text', 'Username contains an invalid character');
			cy.getCy('save-credentials-button').should('have.class', 'base-btn-negative');
			cy.getCy('close-alert-dialog').click();
			cy.getCy('app-username-input').should('have.value', 'Invalid Username');
			cy.get('@getCredentials.all').should('have.length', 1);

			cy.getCy('app-username-input').clear().type('RecoveredUser22');
			cy.getCy('save-credentials-button').should('not.be.disabled').click();
			cy.wait('@saveCredentials').its('response.statusCode').should('equal', 200);
			cy.wrap(null).should(() => expect(backend.heldLoads).to.equal(2));
			cy.getCy('app-username-input').should('be.disabled');
			cy.getCy('app-password-input').should('be.disabled');
			cy.getCy('app-confirm-password-input').should('be.disabled');
			cy.getCy('save-credentials-button').should('be.disabled').click({ force: true });
			cy.then(() => expect(backend.saveAttempts).to.equal(2));
			cy.then(() => backend.release());
			cy.wait('@getCredentials').its('response.body.value.userName').should('equal', 'RecoveredUser22');
			cy.getCy('save-credentials-button').should('have.class', 'base-btn-positive').and('be.disabled');
			cy.getCy('app-username-input').should('not.be.disabled').and('have.value', 'RecoveredUser22');
			cy.reload();
			cy.wait('@getCredentials');
			cy.getCy('app-username-input').should('have.value', 'RecoveredUser22');
		});
	});
});
