import {
	AuthenticationPaths,
	DebugPaths,
	DownloadPaths,
	FolderPathPaths,
	NotificationPaths,
	PlexAccountPaths,
	PlexLibraryPaths,
	PlexServerConnectionPaths,
	PlexServerPaths,
} from '@api-urls';
import { BackgroundJobsPaths } from '@api/generated/BackgroundJobs';
import { IntegrationPaths } from '@api/generated/Integration';
import { headers, route } from '@fixtures';
import { generateFailedResultDTO, generateResultDTO } from '@mock';
import { escapeRegExp } from 'lodash-es';

const failedResult = (statusCode: number, message: string) => generateFailedResultDTO({
	statusCode,
	errors: [{ message, reasons: [], metadata: {} }],
});

const browser = Cypress.browser.name === 'firefox' ? 'Firefox' : 'Google Chrome';

describe('Global API error pipeline', () => {
	beforeEach(() => {
		cy.basePageSetup({ isLoggedIn: false, plexAccountCount: 0, plexServerCount: 0 });
	});

	it('Shows a startup failure on the login page and recovers after reloading', () => {
		const message = 'Startup failed <script>alert("unsafe")</script>';
		cy.intercept('GET', AuthenticationPaths.authenticationStatusEndpoint(), {
			statusCode: 500,
			body: failedResult(500, message),
			headers: {
				...headers.headers,
				'access-control-expose-headers': 'x-reaparr-version,x-reaparr-platform',
				'x-reaparr-version': 'v0.31.0',
				'x-reaparr-platform': 'Linux',
			},
		}).as('startupFailure');

		cy.visit(route('/login?report-test=do-not-share'));
		cy.wait('@startupFailure').its('response.statusCode').should('eq', 500);
		cy.getCy('alert-dialog').should('be.visible').and('have.length', 1);
		cy.getCy('api-error').should('have.length', 1).and('contain.text', 'GET — HTTP 500')
			.and('contain.text', AuthenticationPaths.authenticationStatusEndpoint())
			.and('contain.text', message);
		cy.getCy('alert-dialog').find('script').should('not.exist');
		cy.getCy('report-api-error').should(($link) => {
			const issue = new URL($link.attr('href')!);
			expect(issue.searchParams.get('template')).to.equal('BUG-REPORT.yml');
			expect(issue.searchParams.get('version')).to.equal('v0.31.0');
			expect(issue.searchParams.get('os')).to.equal('Linux');
			expect(issue.searchParams.get('browsers')).to.equal(browser);
			expect(issue.searchParams.get('reproduce-steps')).to.include('Page at report time: /login');
			expect(issue.searchParams.get('logs')).to.include('Captured frontend request failures (not backend logs):')
				.and.include(AuthenticationPaths.authenticationStatusEndpoint());
			expect(issue.href).not.to.include('do-not-share');
			expect(issue.searchParams.has('checks')).to.equal(false);
			expect(issue.searchParams.has('screenshot')).to.equal(false);
		});
		cy.getCy('close-alert-dialog').click();
		cy.getCy('alert-dialog').should('not.exist');

		cy.interceptAuthenticationStatus(false);
		cy.reload();
		cy.getCy('login-submit-button').should('be.visible');
		cy.getCy('alert-dialog').should('not.exist');
	});

	it('Coalesces a startup burst, caps it at ten failures, and reports all retained failures', () => {
		cy.basePageSetup({ isLoggedIn: true, plexAccountCount: 0, plexServerCount: 0 });

		const failures = [
			{ path: PlexAccountPaths.getAllPlexAccountsEndpoint(), status: 400 },
			{ path: BackgroundJobsPaths.getAllBackgroundJobsEndpoint(), status: 403 },
			{ path: DownloadPaths.getAllDownloadTasksEndpoint(), status: 404 },
			{ path: FolderPathPaths.getAllFolderPathsEndpoint(), status: 409 },
			{ path: PlexLibraryPaths.getAllPlexLibrariesEndpoint(), status: 422 },
			{ path: PlexLibraryPaths.getLibrarySyncStatusEndpoint(), status: 429 },
			{ path: DebugPaths.getAllLogsEndpoint(), status: 500 },
			{ path: NotificationPaths.getAllNotificationsEndpoint(), status: 502 },
			{ path: PlexServerConnectionPaths.getAllPlexServerConnectionsEndpoint(), status: 503 },
			{ path: PlexServerPaths.getAllPlexServersEndpoint(), status: 504 },
		];
		for (const [index, failure] of failures.entries()) {
			cy.intercept({ method: 'GET', pathname: failure.path }, {
				statusCode: failure.status,
				body: failedResult(failure.status, `Startup failure: ${failure.path}`),
				headers: {
					...headers.headers,
					'access-control-expose-headers': 'x-reaparr-version,x-reaparr-platform',
				},
			}).as(`startupFailure${index}`);
		}
		// Integration setup runs after the other setup requests complete, making this the eleventh failure.
		cy.intercept('GET', IntegrationPaths.getIntegrationsEndpoint(), {
			statusCode: 500,
			body: failedResult(500, 'Omitted integration failure'),
			...headers,
		}).as('overflowFailure');

		cy.visit(route('/setup'));
		for (const [index, failure] of failures.entries()) {
			cy.wait(`@startupFailure${index}`).should(({ request, response }) => {
				expect(request.method).to.equal('GET');
				expect(response?.statusCode).to.equal(failure.status);
			});
		}
		cy.wait('@overflowFailure').its('response.statusCode').should('eq', 500);
		cy.url().should('eq', route('/setup'));
		cy.getCy('alert-dialog').should('be.visible').and('have.length', 1);
		cy.getCy('api-error').should('have.length', 10);
		for (const failure of failures) {
			cy.contains('[data-cy="api-error"]', new RegExp(`Startup failure: ${escapeRegExp(failure.path)}\\s*$`))
				.should('contain.text', `GET — HTTP ${failure.status}`);
		}
		cy.getCy('api-errors-omitted').should('contain.text', 'Only the first 10 distinct failures are shown');
		cy.getCy('alert-dialog').should('not.contain.text', 'Omitted integration failure');

		cy.getCy('alert-dialog').find('[role="radio"]').should('not.exist');
		cy.getCy('report-api-error').should('have.attr', 'target', '_blank')
			.and('have.attr', 'rel', 'noopener noreferrer').should(($link) => {
				const issue = new URL($link.attr('href')!);
				expect(issue.origin + issue.pathname).to.equal('https://github.com/Reaparr/Reaparr/issues/new');
				expect(issue.searchParams.get('template')).to.equal('BUG-REPORT.yml');
				expect(issue.searchParams.get('title')).to.equal('[BUG] - Reaparr: Something went wrong');
				expect(issue.searchParams.get('version')).to.equal('1.0.0');
				expect(issue.searchParams.get('browsers')).to.equal(browser);
				expect(issue.searchParams.has('os')).to.equal(false);
				expect(issue.searchParams.get('description')).to.include('Deployment: docker');
				expect(issue.searchParams.get('reproduce-steps')).to.include('Page at report time: /setup');
				const logs = issue.searchParams.get('logs');
				for (const failure of failures) {
					expect(logs).to.include(`${failure.path}\nHTTP ${failure.status}`);
				}
				expect(logs).to.include('Only the first 10 distinct failures are shown')
					.and.not.include('Omitted integration failure');
			});
		for (const viewport of [{ width: 1280, height: 720 }, { width: 320, height: 568 }]) {
			cy.viewport(viewport.width, viewport.height);
			cy.getCy('close-alert-dialog').should(($close) => {
				const close = $close[0]!;
				const footer = close.parentElement!;
				const report = footer.querySelector<HTMLElement>('[data-cy="report-api-error"]')!;
				const footerBounds = footer.getBoundingClientRect();
				const closeBounds = close.getBoundingClientRect();
				const reportBounds = report.getBoundingClientRect();

				expect(closeBounds.left, 'Close aligns with the left footer edge').to.be.closeTo(footerBounds.left, 1);
				expect(reportBounds.right, 'Report aligns with the right footer edge').to.be.closeTo(footerBounds.right, 1);
				expect(closeBounds.bottom, 'Close stays within the viewport').to.be.at.most(viewport.height);
				expect(reportBounds.bottom, 'Report stays within the viewport').to.be.at.most(viewport.height);
			});
		}
		cy.viewport(390, 844);
		cy.getCy('close-alert-dialog').should('be.visible').click();
		cy.getCy('alert-dialog').should('not.exist');

		cy.basePageSetup({ isLoggedIn: true, plexAccountCount: 0, plexServerCount: 0 });
		cy.intercept('GET', IntegrationPaths.getIntegrationsEndpoint(), {
			statusCode: 200,
			body: generateResultDTO([]),
		}).as('recoveredIntegrations');
		cy.reload();
		cy.wait('@recoveredIntegrations');
		cy.getCy('page-load-completed').should('be.visible');
		cy.getCy('alert-dialog').should('not.exist');
	});

	it('Shows HTTP errors on submit, reopens repeats, preserves local validation, and recovers from connectivity loss', () => {
		cy.visit(route('/login'));
		cy.getCy('login-username-input').type('admin');
		cy.getCy('login-password-input').type('password');

		for (const status of [400, 403, 500, 500, 503]) {
			cy.intercept('POST', AuthenticationPaths.appUserLoginEndpoint(), {
				statusCode: status,
				body: failedResult(status, `Login failure ${status}`),
			}).as('httpFailure');
			cy.getCy('login-submit-button').click();
			cy.wait('@httpFailure').its('response.statusCode').should('eq', status);
			cy.getCy('alert-dialog').should('be.visible').and('have.length', 1);
			cy.getCy('api-error').should('have.length', 1)
				.and('contain.text', `POST — HTTP ${status}`)
				.and('contain.text', `Login failure ${status}`);
			cy.getCy('api-errors-omitted').should('not.exist');
			cy.getCy('close-alert-dialog').click();
			cy.getCy('alert-dialog').should('not.exist');
			if (status === 403) {
				cy.getCy('login-locked-out-alert').should('be.visible');
			}
		}

		cy.intercept('POST', AuthenticationPaths.appUserLoginEndpoint(), {
			statusCode: 401,
			body: failedResult(401, 'Invalid credentials'),
		}).as('unauthorized');
		cy.getCy('login-submit-button').click();
		cy.wait('@unauthorized').its('response.statusCode').should('eq', 401);
		cy.url().should('eq', route('/login'));
		cy.getCy('login-invalid-credentials-alert').should('be.visible');
		cy.getCy('alert-dialog').should('not.exist');

		cy.intercept('POST', AuthenticationPaths.appUserLoginEndpoint(), {
			statusCode: 200,
			body: failedResult(403, 'Domain validation failure'),
		}).as('domainFailure');
		cy.getCy('login-submit-button').click();
		cy.wait('@domainFailure').its('response.statusCode').should('eq', 200);
		cy.getCy('login-locked-out-alert').should('be.visible');
		cy.getCy('login-invalid-credentials-alert').should('not.exist');
		cy.getCy('alert-dialog').should('not.exist');

		cy.intercept('POST', AuthenticationPaths.appUserLoginEndpoint(), { forceNetworkError: true }).as('networkFailure');
		cy.getCy('login-submit-button').click();
		cy.wait('@networkFailure').should('have.property', 'error');
		cy.contains('[role="alert"]', 'Cannot reach the Reaparr backend. Check your connection and try again.')
			.should('be.visible');
		cy.getCy('alert-dialog').should('not.exist');
		cy.getCy('login-locked-out-alert').should('not.exist');

		cy.intercept('POST', AuthenticationPaths.appUserLoginEndpoint(), {
			statusCode: 200,
			body: generateResultDTO(true),
		}).as('recoveredLogin');
		cy.interceptAuthenticationStatus(true);
		cy.intercept('GET', IntegrationPaths.getIntegrationsEndpoint(), {
			statusCode: 200,
			body: generateResultDTO([]),
		});
		cy.getCy('login-submit-button').click();
		cy.wait('@recoveredLogin').its('response.statusCode').should('eq', 200);
		cy.url().should('eq', route('/'));
		cy.getCy('page-load-completed').should('be.visible');
		cy.getCy('alert-dialog').should('not.exist');
	});
});
