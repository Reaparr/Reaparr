import { route } from '@fixtures';
import { UpdatePaths } from '@api-urls';
import { generateResultDTO } from '@mock';
import { MessageTypes, RefreshDataType } from '@dto';

describe('Desktop update availability refresh', () => {
	beforeEach(() => {
		cy.hubClear();
		// The mock package may retain an older document after navigation.
		Cypress.Commands.overwrite('hubPublish', (_original, hubName, messageType, payload) =>
			cy.window().then((autWindow) => {
				const hub = autWindow['cypress-signalr-mock']?.mocks.find((mock) => mock.name === hubName);
				if (!hub) {
					throw new Error(`SignalR mock hub ${hubName} is not ready in the current page`);
				}
				hub.publish(messageType, payload);
			}),
		);
		cy.intercept({ url: '**/api/**', middleware: true }, (request) => {
			request.on('before:response', (response) => {
				response.headers['x-reaparr-platform'] = 'desktop';
			});
		});
	});

	it('Should check once and show the published version and release notes', () => {
		const availableUpdate = {
			currentVersion: '1.0.0',
			newestVersion: '1.0.1',
			isUpdateAvailable: true,
			releaseNotes: [{
				version: '1.0.1',
				releaseDate: '2026-10-18T00:00:00Z',
				notes: 'SignalR refresh browser coverage',
				isDevRelease: false,
			}],
		};
		const [releaseNote] = availableUpdate.releaseNotes;
		if (!releaseNote) {
			throw new Error('Expected update response to contain a release note');
		}
		const noUpdate = {
			currentVersion: '1.0.0',
			newestVersion: '1.0.0',
			isUpdateAvailable: false,
			releaseNotes: [],
		};
		let checkCalls = 0;

		cy.basePageSetup({ isLoggedIn: true, plexAccountCount: 0, plexServerCount: 0 }).then(() => {
			cy.intercept('GET', UpdatePaths.checkForUpdateEndpoint(), (request) => {
				checkCalls++;
				request.reply({
					statusCode: 200,
					body: generateResultDTO(checkCalls === 1 ? noUpdate : availableUpdate),
				});
			}).as('checkUpdate');

			cy.visit(route('/settings/ui'));
			cy.wait('@checkUpdate');
			cy.getPageData();
			cy.then(() => expect(checkCalls).to.equal(1));
			cy.get('.app-bar .update-button').filter(':visible').should('not.have.class', 'update-button--pulse');

			cy.clock().as('updateClock');
			cy.hubPublish('notifications', MessageTypes.RefreshNotification, RefreshDataType.UpdateAvailable);
			cy.tick(250);
			cy.wait('@checkUpdate').its('response.body.value').should('deep.equal', availableUpdate);
			cy.then(() => expect(checkCalls).to.equal(2));
			cy.get('@updateClock').invoke('restore');

			cy.get('.app-bar .update-button')
				.filter(':visible')
				.should('have.class', 'update-button--pulse')
				.click();
			cy.getCy('updatedialog-cy').should('be.visible').within(() => {
				cy.get('.update-dialog__tab-version').should('have.length', 1).and('have.text', availableUpdate.newestVersion);
				cy.get('.update-dialog__notes').should('contain.text', releaseNote.notes);
				cy.contains('button', 'Download').should('be.visible');
			});
		});
	});

	it('Should show the target version of an available update without release notes', () => {
		const availableUpdate = {
			currentVersion: '1.0.0',
			newestVersion: '1.0.2',
			isUpdateAvailable: true,
			releaseNotes: [],
		};
		cy.basePageSetup({ isLoggedIn: true, plexAccountCount: 0, plexServerCount: 0 }).then(() => {
			cy.intercept('GET', UpdatePaths.checkForUpdateEndpoint(), {
				statusCode: 200, body: generateResultDTO(availableUpdate),
			}).as('checkUpdate');
			cy.visit(route('/settings/ui'));
			cy.wait('@checkUpdate');
			cy.getPageData();
			cy.get('.app-bar .update-button').filter(':visible').should('have.class', 'update-button--pulse').click();

			cy.getCy('updatedialog-cy').should('be.visible').within(() => {
				cy.get('.update-dialog__title-versions').should('contain.text', availableUpdate.newestVersion).and('not.contain.text', '?');
				cy.get('.update-dialog__tab-version').should('not.exist');
				cy.get('.update-dialog__notes').should('not.exist');
				cy.contains('button', 'Download').should('be.visible').and('not.be.disabled');
			});
		});
	});
});
