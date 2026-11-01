import { headers } from '@fixtures';
import { PlexServerConnectionPaths } from '@api-urls';
import { generateResultDTO } from '@mock';
import type { PlexServerStatusDTO } from '@dto';

describe('First manual server connection check', () => {
	it('updates an unchecked row immediately and displays a later failure without refreshing the connection list', () => {
		// Arrange
		cy.viewport(1280, 800);
		cy.basePageSetup({
			seed: 80770, plexAccountCount: 1, plexServerCount: 1, maxServerConnections: 1,
			override: { plexServerConnections: (connections) => connections.map((connection) => ({ ...connection, latestConnectionStatus: null })) },
		}).then(({ plexServerConnections }) => {
			const connection = plexServerConnections[0]!;
			let listReads = 0;
			let readsBeforeChecks = 0;
			let checks = 0;
			cy.intercept('GET', PlexServerConnectionPaths.getAllPlexServerConnectionsEndpoint(), (request) => {
				listReads++;
				request.reply({ statusCode: 200, body: generateResultDTO(plexServerConnections), ...headers });
			});
			cy.intercept('GET', PlexServerConnectionPaths.checkConnectionStatusByIdEndpoint(connection.id), (request) => {
				const connected = checks++ === 0;
				const status: PlexServerStatusDTO = {
					id: checks, plexServerId: connection.plexServerId, plexServerConnectionId: connection.id,
					isSuccessful: connected, lastChecked: '2026-10-20T12:00:00Z',
					statusCode: connected ? 200 : 503, statusMessage: connected ? 'Completed' : 'Unavailable',
				};
				connection.latestConnectionStatus = status;
				request.reply({ statusCode: 200, body: generateResultDTO(status), ...headers });
			}).as('manualCheck');
			cy.visitEmptyPage();
			cy.getCy('page-load-completed', { timeout: 20000 }).should('be.visible');
			cy.getCy(`server-drawer-item-${connection.plexServerId}`).filter(':visible').first()
				.closest('.q-item').find('[data-cy^="server-dialog-"]').click();
			cy.getCy('server-dialog-tab-2').click();
			const button = `check-connection-btn-${connection.id}`;
			const row = () => cy.getCy(button).closest('.server-connection-row');
			row().find('[data-cy="status-indicator-offline"]').should('be.visible').and('have.attr', 'negative');
			cy.then(() => {
				readsBeforeChecks = listReads;
			});

			// Act
			cy.getCy(button).click();
			cy.wait('@manualCheck').its('response.body.value.isSuccessful').should('eq', true);

			// Assert
			row().find('[data-cy="status-indicator-online"]').should('be.visible').and('have.attr', 'positive');
			cy.getCy(button).should('not.be.disabled');

			// Act
			cy.getCy(button).click();
			cy.wait('@manualCheck').its('response.body.value.isSuccessful').should('eq', false);

			// Assert
			row().find('[data-cy="status-indicator-offline"]').should('be.visible').and('have.attr', 'negative');
			cy.getCy(button).should('not.be.disabled');
			cy.then(() => {
				expect(checks).to.equal(2);
				expect(listReads, 'no connection-list refresh is needed to display check results').to.equal(readsBeforeChecks);
			});
		});
	});
});
