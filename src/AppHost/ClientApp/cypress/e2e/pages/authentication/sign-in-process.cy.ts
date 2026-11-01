import { route } from '@fixtures';
import { AuthenticationPaths, SettingsPaths } from '@api-urls';
import { generateFailedResultDTO, generateResultDTO } from '@mock';
import type { AppUserLoginEndpointRequest, SettingsModelDTO } from '@dto';

function holdLoginInitialization(settings: SettingsModelDTO) {
	let loginRequests = 0;
	let initializationRequested = false;
	const loginResponses: Array<() => void> = [];
	const initializationResponses: Array<() => void> = [];
	cy.intercept('POST', AuthenticationPaths.appUserLoginEndpoint(), (request) => {
		loginRequests++;
		return new Promise<void>((resolve) => {
			loginResponses.push(() => {
				request.reply(200, generateResultDTO<AppUserLoginEndpointRequest>({
					username: 'admin', password: 'password', rememberMe: true,
				}));
				resolve();
			});
		});
	}).as('loginSuccess');
	cy.intercept({ method: 'GET', url: SettingsPaths.getUserSettingsEndpoint(), times: 1 }, (request) => {
		initializationRequested = true;
		return new Promise<void>((resolve) => {
			initializationResponses.push(() => {
				request.reply(200, generateResultDTO(settings));
				resolve();
			});
		});
	}).as('loginInitialization');
	return {
		get loginRequests() { return loginRequests; },
		get initializationRequested() { return initializationRequested; },
		releaseLogin: () => loginResponses.splice(0).forEach((reply) => reply()),
		releaseInitialization: () => initializationResponses.splice(0).forEach((reply) => reply()),
	};
}

describe('sign-in-process', () => {
	let releasePending: (() => void) | undefined;
	afterEach(() => {
		releasePending?.();
		releasePending = undefined;
	});

	it('Should redirect to the login page when not logged in and then to the home page when logged in', () => {
		cy.basePageSetup({
			plexServerCount: 1,
			plexAccountCount: 1,
			plexMovieLibraryCount: 1,
			movieCount: 100,
			isLoggedIn: false,
		});

		cy.visit(route('/'));
		cy.url().should('eq', route('/login'));
		cy.getCy('login-submit-button').should('be.visible');

		cy.getPageData().then(({ settings }) => {
			const responses = holdLoginInitialization(settings);
			releasePending = () => {
				responses.releaseLogin();
				responses.releaseInitialization();
			};
			cy.interceptAuthenticationStatus(true);

			// Type credentials
			cy.getCy('login-username-input').type('admin');
			cy.getCy('login-password-input').type('password');
			cy.getCy('login-remember-me-input').click();
			cy.getCy('login-submit-button').click();
			cy.getCy('login-submit-button').should('be.disabled').click({ force: true });
			cy.getCy('login-username-input').should('be.disabled');
			cy.getCy('login-password-input').should('be.disabled');
			cy.getCy('login-remember-me-input').should('have.attr', 'aria-disabled', 'true');
			cy.then(() => expect(responses.loginRequests).to.equal(1));
			cy.then(() => responses.releaseLogin());
			cy.wait('@loginSuccess').its('response.statusCode').should('equal', 200);
			cy.wrap(null).should(() => expect(responses.initializationRequested).to.equal(true));
			cy.getCy('login-submit-button').should('be.disabled').click({ force: true });
			cy.then(() => expect(responses.loginRequests).to.equal(1));
			cy.then(() => responses.releaseInitialization());
			cy.wait('@loginInitialization').its('response.statusCode').should('equal', 200);

			cy.url().should('eq', route('/'));
		});
	});

	it('Should show authentication failures and recover with valid credentials', () => {
		cy.basePageSetup({
			isLoggedIn: false,
		});

		cy.visit(route('/'));
		cy.url().should('eq', route('/login'));

		cy.getPageData().then(() => {
			// Login
			cy.intercept('POST', AuthenticationPaths.appUserLoginEndpoint(), {
				statusCode: 401,
				body: generateFailedResultDTO({
					statusCode: 401,
					errors: [{ message: 'Invalid credentials', reasons: [], metadata: {} }],
				}),
			}).as('loginUnauthorized');
			cy.interceptAuthenticationStatus(false);

			// Type credentials
			cy.getCy('login-username-input').type('admin');
			cy.getCy('login-password-input').type('password');
			cy.getCy('login-remember-me-input').click();
			cy.getCy('login-submit-button').click();
			cy.wait('@loginUnauthorized');

			cy.url().should('eq', route('/login'));
			cy.getCy('login-invalid-credentials-alert').should('be.visible');
			cy.getCy('alert-dialog').should('not.exist');

			// Submit 3 times to lock account
			cy.getCy('login-submit-button').click();
			cy.wait('@loginUnauthorized');
			cy.getCy('login-submit-button').click();
			cy.wait('@loginUnauthorized');

			cy.intercept('POST', AuthenticationPaths.appUserLoginEndpoint(), {
				statusCode: 403,
				body: generateFailedResultDTO({
					statusCode: 403,
					errors: [{ message: 'Account locked', reasons: [], metadata: {} }],
				}),
			}).as('loginLocked');

			cy.getCy('login-submit-button').click();
			cy.wait('@loginLocked');
			cy.getCy('alert-dialog').should('be.visible');
			cy.getCy('api-error').should('have.length', 1)
				.and('contain.text', 'POST — HTTP 403')
				.and('contain.text', AuthenticationPaths.appUserLoginEndpoint())
				.and('contain.text', 'Account locked');
			cy.getCy('close-alert-dialog').click();
			cy.getCy('alert-dialog').should('not.exist');

			cy.url().should('eq', route('/login'));
			cy.getCy('login-locked-out-alert').should('be.visible');

			cy.intercept('POST', AuthenticationPaths.appUserLoginEndpoint(), {
				statusCode: 200,
				body: generateResultDTO<AppUserLoginEndpointRequest>({
					username: 'admin',
					password: 'password',
					rememberMe: true,
				}),
			}).as('loginRecovered');
			cy.interceptAuthenticationStatus(true);
			cy.getCy('login-submit-button').click();
			cy.wait('@loginRecovered').its('response.statusCode').should('equal', 200);
			cy.url().should('eq', route('/'));
		});
	});

	it('Should retain the session after failed logout and finish a confirmed retry through reload', () => {
		cy.basePageSetup({ isLoggedIn: true, plexAccountCount: 1, plexServerCount: 1 });
		let authenticated = true;
		let logoutRequests = 0;
		const held = Promise.withResolvers<null>();
		releasePending = () => held.resolve(null);
		cy.intercept('GET', AuthenticationPaths.authenticationStatusEndpoint(), (request) => {
			request.alias = authenticated ? 'authenticatedLogoutStatus' : 'loggedOutStatus';
			request.reply({
				statusCode: authenticated ? 200 : 401,
				body: authenticated
					? generateResultDTO({ isLoggedIn: true, userName: 'admin', claims: [] })
					: generateFailedResultDTO({ statusCode: 401 }),
			});
		});
		cy.intercept('POST', AuthenticationPaths.appUserLogOutEndpoint(), (request) => {
			if (++logoutRequests === 1) {
				return held.promise.then(() => request.reply({
					statusCode: 500,
					body: generateFailedResultDTO({
						statusCode: 500, errors: [{ message: 'Logout not confirmed', reasons: [], metadata: {} }],
					}),
				}));
			}
			authenticated = false;
			request.reply({ statusCode: 200, body: generateResultDTO(null) });
		}).as('logout');

		cy.visit(route('/'));
		cy.wait('@authenticatedLogoutStatus').its('response.statusCode').should('equal', 200);
		cy.getCy('account-selector-btn').should('be.visible').click();
		cy.contains('Logout').should('be.visible').click();
		cy.wrap(null).should(() => expect(logoutRequests).to.equal(1));
		cy.url().should('eq', route('/'));
		cy.getCy('login-submit-button').should('not.exist');
		cy.then(() => held.resolve(null));
		cy.wait('@logout').its('response.statusCode').should('equal', 500);
		cy.getCy('alert-dialog').should('be.visible').and('contain.text', 'Logout not confirmed');
		cy.url().should('eq', route('/'));
		cy.getCy('close-alert-dialog').click();
		cy.reload();
		cy.wait('@authenticatedLogoutStatus').its('response.statusCode').should('equal', 200);
		cy.url().should('eq', route('/'));
		cy.getCy('account-selector-btn').should('be.visible').click();
		cy.contains('Logout').should('be.visible').click();
		cy.wait('@logout').its('response.statusCode').should('equal', 200);
		cy.url().should('eq', route('/login'));
		cy.reload();
		cy.wait('@loggedOutStatus').its('response.statusCode').should('equal', 401);
		cy.url().should('eq', route('/login'));
		cy.getCy('login-submit-button').should('be.visible').and('not.be.disabled');
	});
});
