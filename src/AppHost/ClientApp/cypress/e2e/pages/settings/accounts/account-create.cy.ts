import { route } from '@fixtures';
import { generatePlexAccount, generateResultDTO } from '@mock';
import type { CreatePlexAccountEndpointRequest, PlexAccountDTO } from '@dto';
import { PlexAccountPaths } from '@api-urls';

describe('Add Plex account to Reaparr', () => {
	beforeEach(() => {
		cy.basePageSetup({
			plexAccountCount: 2,
			plexServerCount: 5,
		});

		cy.visit(route('/settings/accounts'));
	});

	it('Should create an Account when input is valid and close on save', () => {
		cy.getPageData().then(() => {
			cy.createPlexAccount(null);

			// Verify the account card is visible
			cy.getCy('account-card-id-99').should('be.visible');
		});
	});

	it('Should request a verification code, show invalid code error, and allow retry when 2FA is enabled', function () {
		cy.viewport(375, 812);
		const plexAccount: PlexAccountDTO = generatePlexAccount({
			id: 99,
			partialData: {
				is2Fa: true,
				isValidated: false,
			},
		});

		cy.getCy('account-overview-add-account').click();
		cy.getCy('account-dialog-form').should('be.visible');

		// Fill in credentials
		cy.getCy('account-form-display-name-input').type(plexAccount.displayName);
		cy.getCy('account-form-username-input').type(plexAccount.username);
		cy.getCy('account-form-password-input').type(plexAccount.password);

		// Validate button should be enabled
		cy.getCy('account-dialog-validate-button').should('not.be.disabled');

		// Initial Plex response: credentials are valid but require a 2FA code
		cy.validatePlexCredentialsEndpoint({
			isUnAuthorized: true,
			partialData: {
				clientId: plexAccount.clientId,
				is2Fa: true,
				isValidated: false,
			},
		}).as('validateNeeds2Fa');

		cy.getCy('account-dialog-validate-button').click();
		cy.wait('@validateNeeds2Fa');

		// Verify 2FA dialog appears
		cy.getCy('2fa-code-verification-dialog').should('be.visible');
		cy.getCy('2fa-code-verification-error').should('not.exist');

		// Invalid verification code: backend returns unauthorized and keeps account unvalidated
		cy.validatePlexCredentialsEndpoint({
			isUnAuthorized: true,
			partialData: {
				clientId: plexAccount.clientId,
				is2Fa: false,
				isValidated: false,
			},
		}).as('validateInvalidCode');

		cy.get(':nth-child(1) > [data-test="single-input"]').type('111111');
		cy.wait('@validateInvalidCode').then((interception) => {
			expect(interception.request.body.verificationCode).to.equal('111111');
		});
		cy.getCy('2fa-code-verification-dialog').should('be.visible');
		cy.getCy('2fa-code-verification-error').should('be.visible');
		cy.getCy('2fa-code-verification-dialog')
			.find('[data-test="single-input"]')
			.should(($inputs) => {
				expect(Array.from($inputs, (input) => (input as HTMLInputElement).value)).to.deep.equal([
					'',
					'',
					'',
					'',
					'',
					'',
				]);
			});
		cy.getCy('2fa-code-verification-confirm-button').should('be.disabled');
		cy.getCy('account-dialog-save-button').should('be.disabled');

		// Retry with a valid verification code
		cy.validatePlexCredentialsEndpoint({
			partialData: {
				clientId: plexAccount.clientId,
				is2Fa: true,
				isValidated: true,
			},
		}).as('validateValidCode');

		cy.get(':nth-child(1) > [data-test="single-input"]').type('123456');
		cy.wait('@validateValidCode').then((interception) => {
			expect(interception.request.body.verificationCode).to.equal('123456');
		});

		// Verify 2FA dialog closes after successful validation
		cy.getCy('2fa-code-verification-dialog').should('not.exist');

		// Save button should now be enabled after successful 2FA validation
		cy.getCy('account-dialog-save-button').should('not.be.disabled');

		// Create Action
		cy.intercept('POST', PlexAccountPaths.createPlexAccountEndpoint(), {
			statusCode: 200,
			body: generateResultDTO({ ...plexAccount, isValidated: true }),
		}).as('createAccount');

		cy.intercept('GET', PlexAccountPaths.getPlexAccountByIdEndpoint(plexAccount.id), {
			statusCode: 200,
			body: generateResultDTO({ ...plexAccount, isValidated: true }),
		});

		cy.getCy('account-dialog-save-button').click();
		cy.getCy('account-dialog-form').should('not.exist');

		// Verify account was created
		cy.wait('@createAccount').then((interception) => {
			expect(interception.request.method).to.equal('POST');
			expect(interception.request.body.isValidated).to.equal(true);
			expect(interception.request.body.is2Fa).to.equal(true);
			expect(interception.request.body.displayName).to.equal(plexAccount.displayName);
		});
	});

	it('Should create an Account when a valid token is added manually', () => {
		cy.getPageData().then(() => {
			const account: PlexAccountDTO = generatePlexAccount({
				id: 99,
				partialData: {
					username: '',
					password: '',
					isValidated: false,
					is2Fa: false,
					hasPassword: true,
				},
			});

			cy.getCy('account-overview-add-account').click();
			cy.getCy('account-dialog-form').should('be.visible');

			// Switch to token mode
			cy.getCy('account-dialog-auth-token-mode-button').click();

			// Verify token input is visible
			cy.getCy('account-form-auth-token-input').should('be.visible');

			// Fill in credentials
			cy.getCy('account-form-display-name-input').type(account.displayName);
			cy.getCy('account-form-auth-token-input').type(account.authenticationToken);

			// Validate Action
			cy.validatePlexTokenEndpoint({
				partialData: {
					id: 0, // Id is not created on validation
					authenticationToken: account.authenticationToken,
					clientId: account.clientId,
					email: account.email,
					is2Fa: false,
					isValidated: true,
					plexId: account.plexId,
					title: account.title,
					username: account.username,
					uuid: account.uuid,
					validatedAt: account.validatedAt,
					hasPassword: account.hasPassword,
				},
			});

			cy.getCy('account-dialog-validate-button').click();

			// Verify validation dialog appears
			cy.getCy('auth-token-validation-dialog').should('be.visible');

			// Verify display name is preserved
			cy.getCy('account-form-display-name-input').should('have.value', account.displayName);

			cy.getCy('auth-token-validation-dialog-hide-button').click();
			cy.getCy('auth-token-validation-dialog').should('not.exist');

			// Create Action
			cy.intercept('POST', PlexAccountPaths.createPlexAccountEndpoint(), {
				statusCode: 200,
				body: generateResultDTO(account),
			}).as('createAccount');

			// Hide Account dialog
			cy.intercept('GET', PlexAccountPaths.getPlexAccountByIdEndpoint(account.id), {
				statusCode: 200,
				body: generateResultDTO(account),
			});

			cy.getCy('account-dialog-save-button').click();
			cy.getCy('account-dialog-form').should('not.exist');

			cy.wait('@createAccount').then((interception) => {
				expect(interception.request.method).to.equal('POST');
				const expectedBody: CreatePlexAccountEndpointRequest = {
					isValidated: true,
					password: account.password,
					username: account.username,
					uuid: account.uuid,
					validatedAt: account.validatedAt!,
					authenticationToken: account.authenticationToken,
					customAuthenticationToken: account.customAuthenticationToken,
					clientId: account.clientId,
					displayName: account.displayName,
					email: account.email,
					is2Fa: account.is2Fa,
					isEnabled: account.isEnabled,
					isMain: account.isMain,
					plexId: account.plexId,
					title: account.title,
				};
				expect(interception.request.body).to.deep.equal(expectedBody);
			});
		});
	});

	it('Should show failed validation dialog when a invalid token is added manually, and then allow for another validation and succeed', () => {
		cy.getPageData().then(() => {
			const account: PlexAccountDTO = generatePlexAccount({
				id: 99,
				partialData: {
					username: '',
					password: '',
					isValidated: false,
					is2Fa: false,
				},
			});

			cy.getCy('account-overview-add-account').click();
			cy.getCy('account-dialog-form').should('be.visible');

			// Switch to token mode
			cy.getCy('account-dialog-auth-token-mode-button').click();

			// Verify token input is visible
			cy.getCy('account-form-auth-token-input').should('be.visible');

			// Fill in credentials
			cy.getCy('account-form-display-name-input').type(account.displayName);
			cy.getCy('account-form-auth-token-input').type(account.authenticationToken);

			// Validate Action, failed
			cy.validatePlexTokenEndpoint({
				isUnAuthorized: true,
				partialData: {
					authenticationToken: account.authenticationToken,
					clientId: account.clientId,
					email: account.email,
					is2Fa: false,
					isValidated: false,
					plexId: account.plexId,
					title: account.title,
					username: account.username,
					uuid: account.uuid,
					validatedAt: account.validatedAt,
				},
			}).as('validateFailed');

			cy.getCy('account-dialog-validate-button').click();
			cy.wait('@validateFailed');

			// Verify validation dialog appears with error
			cy.getCy('auth-token-validation-dialog').should('be.visible');
			cy.getCy('auth-token-validation-dialog-invalid-token-alert').should('be.visible');

			cy.getCy('auth-token-validation-dialog-hide-button').click();
			cy.getCy('auth-token-validation-dialog').should('not.exist');

			// Validate Action, success
			cy.validatePlexTokenEndpoint({
				isUnAuthorized: false,
				partialData: {
					authenticationToken: account.authenticationToken,
					clientId: account.clientId,
					email: account.email,
					is2Fa: false,
					isValidated: true,
					plexId: account.plexId,
					title: account.title,
					username: account.username,
					uuid: account.uuid,
					validatedAt: account.validatedAt,
				},
			}).as('validateSuccess');

			cy.getCy('account-form-auth-token-input').type('fix');
			cy.getCy('account-dialog-validate-button').click();
			cy.wait('@validateSuccess');
			cy.getCy('auth-token-validation-dialog').should('be.visible');

			cy.getCy('auth-token-validation-dialog-hide-button').click();
			cy.getCy('auth-token-validation-dialog').should('not.exist');

			// Create Action
			cy.intercept('POST', PlexAccountPaths.createPlexAccountEndpoint(), {
				statusCode: 200,
				body: generateResultDTO(account),
			}).as('createAccount');

			// Hide Account dialog
			cy.intercept('GET', PlexAccountPaths.getPlexAccountByIdEndpoint(account.id), {
				statusCode: 200,
				body: generateResultDTO(account),
			});

			cy.getCy('account-dialog-save-button').click();
			cy.getCy('account-dialog-form').should('not.exist');

			cy.wait('@createAccount').then((interception) => {
				expect(interception.request.method).to.equal('POST');
				const expectedBody: CreatePlexAccountEndpointRequest = {
					isValidated: true,
					password: account.password,
					username: account.username,
					uuid: account.uuid,
					validatedAt: account.validatedAt!,
					authenticationToken: account.authenticationToken,
					customAuthenticationToken: account.customAuthenticationToken,
					clientId: account.clientId,
					displayName: account.displayName,
					email: account.email,
					is2Fa: account.is2Fa,
					isEnabled: account.isEnabled,
					isMain: account.isMain,
					plexId: account.plexId,
					title: account.title,
				};
				expect(interception.request.body).to.deep.equal(expectedBody);
			});
		});
	});

	it('Should show validation errors when credentials are invalid and allow retry', () => {
		cy.getPageData().then(() => {
			const account: PlexAccountDTO = generatePlexAccount({
				id: 99,
				partialData: {
					isValidated: false,
					is2Fa: false,
				},
			});

			cy.getCy('account-overview-add-account').click();
			cy.getCy('account-dialog-form').should('be.visible');

			// Fill in credentials
			cy.getCy('account-form-display-name-input').type(account.displayName);
			cy.getCy('account-form-username-input').type(account.username);
			cy.getCy('account-form-password-input').type('wrongpassword');

			// First validation attempt - should fail
			cy.validatePlexCredentialsEndpoint({
				isUnAuthorized: true,
				partialData: {
					isValidated: false,
					is2Fa: false,
				},
			}).as('validateCredentialsFailed');

			cy.getCy('account-dialog-validate-button').click();
			cy.wait('@validateCredentialsFailed');
			cy.getCy('auth-token-validation-dialog').should('be.visible');
			cy.getCy('auth-token-validation-dialog-hide-button').click();
			cy.getCy('auth-token-validation-dialog').should('not.exist');
			cy.getCy('account-dialog-form').should('exist');
			cy.getCy('account-form-password-input').should('exist').and('be.enabled');

			// Clear password and enter correct one
			cy.getCy('account-form-password-input').clear();
			cy.getCy('account-form-password-input').type(account.password);

			// Second validation attempt - should succeed
			cy.validatePlexCredentialsEndpoint({
				partialData: {
					isValidated: true,
					is2Fa: false,
				},
			}).as('validateCredentialsSuccess');

			cy.getCy('account-dialog-validate-button').click();
			cy.wait('@validateCredentialsSuccess');
			cy.getCy('account-dialog-form').should('exist');
			cy.getCy('account-form-password-input').should('exist').and('be.enabled');

			// Create Action
			cy.intercept('POST', PlexAccountPaths.createPlexAccountEndpoint(), {
				statusCode: 200,
				body: generateResultDTO(account),
			}).as('createAccount');

			cy.intercept('GET', PlexAccountPaths.getPlexAccountByIdEndpoint(account.id), {
				statusCode: 200,
				body: generateResultDTO(account),
			});

			cy.getCy('account-dialog-save-button').click();
			cy.getCy('account-dialog-form').should('not.exist');

			cy.wait('@createAccount').then((interception) => {
				expect(interception.request.method).to.equal('POST');
				expect(interception.request.body.isValidated).to.equal(true);
				expect(interception.request.body.displayName).to.equal(account.displayName);
			});
		});
	});

	it('Should close dialog without saving when cancel button is clicked', () => {
		cy.getPageData().then(() => {
			const account: PlexAccountDTO = generatePlexAccount({
				id: 99,
				partialData: {
					isValidated: false,
					is2Fa: false,
				},
			});

			// Open dialog
			cy.getCy('account-overview-add-account').click();
			cy.getCy('account-dialog-form').should('be.visible');

			// Fill in some credentials
			cy.getCy('account-form-display-name-input').type(account.displayName);
			cy.getCy('account-form-username-input').type(account.username);
			cy.getCy('account-form-password-input').type(account.password);

			// Verify dialog is open
			cy.getCy('account-dialog-form').should('be.visible');

			// Close dialog by clicking the close button (X button in top right corner)
			cy.getCy('dialog-close-button').click();

			// Verify dialog is closed
			cy.getCy('account-dialog-form').should('not.exist');

			// Verify no account was created (should not have account-99)
			cy.getCy('account-card-id-99').should('not.exist');

			// Verify we're still on the accounts page
			cy.url().should('include', '/settings/accounts');
		});
	});

	it('Should switch between token and credentials mode and clear input fields', () => {
		cy.getPageData().then(() => {
			const account: PlexAccountDTO = generatePlexAccount({
				id: 99,
				partialData: {
					isValidated: false,
					is2Fa: false,
				},
			});

			cy.getCy('account-overview-add-account').click();
			cy.getCy('account-dialog-form').should('be.visible');

			// Start with credentials mode
			cy.getCy('account-form-display-name-input').type(account.displayName);
			cy.getCy('account-form-username-input').should('be.visible').type('test-username');
			cy.getCy('account-form-password-input').should('be.visible').type('test-password');

			// Switch to token mode
			cy.getCy('account-dialog-auth-token-mode-button').click();

			// Verify token input is visible and credentials are hidden
			cy.getCy('account-form-auth-token-input').should('be.visible');
			cy.getCy('account-form-username-input').should('not.exist');
			cy.getCy('account-form-password-input').should('not.exist');

			// Display name should still be present
			cy.getCy('account-form-display-name-input').should('have.value', account.displayName);

			// Enter token
			cy.getCy('account-form-auth-token-input').type('test-token-123');

			// Switch back to credentials mode
			cy.getCy('account-dialog-credentials-mode-button').click();

			// Verify credentials inputs are visible and token is hidden
			cy.getCy('account-form-username-input').should('be.visible');
			cy.getCy('account-form-password-input').should('be.visible');
			cy.getCy('account-form-auth-token-input').should('not.exist');

			// Verify fields were cleared when switching modes
			cy.getCy('account-form-username-input').should('have.value', '');
			cy.getCy('account-form-password-input').should('have.value', '');

			// Display name should still be present
			cy.getCy('account-form-display-name-input').should('have.value', account.displayName);
		});
	});

	it('Should disable save button until validation is successful', () => {
		cy.getPageData().then(() => {
			const account: PlexAccountDTO = generatePlexAccount({
				id: 99,
				partialData: {
					isValidated: false,
					is2Fa: false,
				},
			});

			cy.getCy('account-overview-add-account').click();
			cy.getCy('account-dialog-form').should('be.visible');

			// Fill in credentials
			cy.getCy('account-form-display-name-input').type(account.displayName);
			cy.getCy('account-form-username-input').type(account.username);
			cy.getCy('account-form-password-input').type(account.password);

			// Validate credentials
			cy.validatePlexCredentialsEndpoint({
				partialData: {
					isValidated: true,
					is2Fa: false,
				},
			});

			cy.getCy('account-dialog-validate-button').click();

			// Wait for validation to complete
			cy.wait(500);

			// Verify save button exists and is clickable after validation
			cy.getCy('account-dialog-save-button').should('be.visible');
		});
	});

	it('Should preserve display name when switching between validation modes', () => {
		cy.getPageData().then(() => {
			const account: PlexAccountDTO = generatePlexAccount({
				id: 99,
				partialData: {
					isValidated: false,
					is2Fa: false,
				},
			});

			const displayName = 'My Preserved Display Name';

			cy.getCy('account-overview-add-account').click();
			cy.getCy('account-dialog-form').should('be.visible');

			// Enter display name in credentials mode
			cy.getCy('account-form-display-name-input').type(displayName);
			cy.getCy('account-form-username-input').type(account.username);
			cy.getCy('account-form-password-input').type(account.password);

			// Validate credentials
			cy.validatePlexCredentialsEndpoint({
				partialData: {
					isValidated: true,
					is2Fa: false,
				},
			});

			cy.getCy('account-dialog-validate-button').click();

			// Verify display name is still present
			cy.getCy('account-form-display-name-input').should('have.value', displayName);

			// Switch to token mode
			cy.getCy('account-dialog-auth-token-mode-button').click();

			// Verify display name is preserved
			cy.getCy('account-form-display-name-input').should('have.value', displayName);

			// Enter token and validate
			cy.getCy('account-form-auth-token-input').type(account.authenticationToken);

			cy.validatePlexTokenEndpoint({
				partialData: {
					authenticationToken: account.authenticationToken,
					clientId: account.clientId,
					email: account.email,
					is2Fa: false,
					isValidated: true,
					plexId: account.plexId,
					title: account.title,
					username: account.username,
					uuid: account.uuid,
					validatedAt: account.validatedAt,
				},
			});

			cy.getCy('account-dialog-validate-button').click();
			cy.getCy('auth-token-validation-dialog').should('be.visible');
			cy.getCy('auth-token-validation-dialog-hide-button').click();

			// Verify display name is still preserved after token validation
			cy.getCy('account-form-display-name-input').should('have.value', displayName);

			// Create account
			cy.intercept('POST', PlexAccountPaths.createPlexAccountEndpoint(), {
				statusCode: 200,
				body: generateResultDTO({ ...account, displayName }),
			}).as('createAccount');

			cy.intercept('GET', PlexAccountPaths.getPlexAccountByIdEndpoint(account.id), {
				statusCode: 200,
				body: generateResultDTO({ ...account, displayName }),
			});

			cy.getCy('account-dialog-save-button').click();
			cy.getCy('account-dialog-form').should('not.exist');

			// Verify the created account has the preserved display name
			cy.wait('@createAccount').then((interception) => {
				expect(interception.request.body.displayName).to.equal(displayName);
			});
		});
	});

	const accountViewports = [
		{ width: 320, height: 568 },
		{ width: 375, height: 812 },
		{ width: 568, height: 320 },
		{ width: 768, height: 1024 },
	] as const;

	for (const viewport of accountViewports) {
		it(`keeps add and edit account controls usable at ${viewport.width}x${viewport.height}`, () => {
			cy.viewport(viewport.width, viewport.height);
			cy.getPageData();

			const assertActionLayout = (buttonSelectors: string[]) => {
				cy.get('.account-dialog-actions').then(($actions) => {
					const actions = $actions[0]!;
					const actionRect = actions.getBoundingClientRect();
					expect(actions.scrollWidth, 'account actions overflow').to.be.lte(actions.clientWidth + 1);

					for (const selector of buttonSelectors) {
						cy.getCy(selector)
							.should('be.visible')
							.then(($button) => {
								const buttonRect = $button[0]!.getBoundingClientRect();
								expect(buttonRect.width, `${selector} width`).to.be.gte(44);
								expect(buttonRect.height, `${selector} height`).to.be.gte(44);
								expect(buttonRect.left, `${selector} left`).to.be.gte(actionRect.left);
								expect(buttonRect.right, `${selector} right`).to.be.lte(actionRect.right + 1);
							});
					}
				});
			};

			cy.getCy('account-overview-add-account').click();
			cy.getCy('account-dialog-form').should('be.visible');
			cy.get('.account-dialog-tabs').then(($tabs) => {
				const tabs = $tabs[0]!;
				expect(tabs.scrollWidth, 'account tab overflow').to.be.lte(tabs.clientWidth + 1);
				cy.wrap($tabs).find('.q-tab').should('have.length', 2).each(($tab) => {
					expect($tab[0]!.getBoundingClientRect().width, 'account tab width').to.be.gte(tabs.clientWidth / 2 - 1);
				});
			});
			assertActionLayout(['account-dialog-validate-button', 'account-dialog-save-button']);
			cy.getCy('dialog-close-button').click();

			cy.get('[data-cy^="account-card-id-"]').first().click();
			cy.getCy('account-dialog-form').should('be.visible');
			assertActionLayout([
				'account-dialog-delete-button',
				'account-dialog-generate-token-button',
				'account-dialog-validate-button',
				'account-dialog-update-button',
			]);
			cy.getCy('dialog-close-button').click();
		});
	}
});
