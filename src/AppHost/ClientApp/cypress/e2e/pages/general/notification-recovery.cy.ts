import { route } from '@fixtures';
import { NotificationPaths } from '@api-urls';
import { generateFailedResultDTO, generateResultDTO } from '@mock';
import { NotificationLevel, type NotificationDTO } from '@dto';
import { HubConnectionState } from '@microsoft/signalr';

const notifications: NotificationDTO[] = [1, 2].map((id) => ({
	id, level: NotificationLevel.Information, createdAt: '2026-10-19T00:00:00Z', message: `Notification ${id}`, hidden: false,
}));

function installReconnectFixture(autWindow: Window) {
	const mocks: Window['cypress-signalr-mock']['mocks'] = [];
	mocks.push = (...hubs) => {
		for (const hub of hubs) {
			Object.assign(hub, { state: HubConnectionState.Connected });
			hub.onreconnected = (callback) => hub.on('FixtureReconnect', () => callback('fixture-connection'));
		}
		return Array.prototype.push.apply(mocks, hubs);
	};
	autWindow['cypress-signalr-mock'] = { mocks };
}

describe('Notification action recovery', () => {
	beforeEach(() => {
		cy.hubClear();
		Cypress.Commands.overwrite('hubPublish', (_original, hubName, messageType, payload) =>
			cy.window().then((autWindow) => {
				const hub = autWindow['cypress-signalr-mock']?.mocks.find((mock) => mock.name === hubName);
				if (!hub) throw new Error(`SignalR mock hub ${hubName} is not ready in the current page`);
				hub.publish(messageType, payload);
			}),
		);
		cy.basePageSetup({ plexAccountCount: 0, plexServerCount: 0 }).then(() => {
			cy.intercept('GET', NotificationPaths.getAllNotificationsEndpoint(), {
				statusCode: 200, body: generateResultDTO(notifications),
			}).as('notifications');
			cy.visit(route('/settings/ui'), { onBeforeLoad: installReconnectFixture });
			cy.wait('@notifications');
			cy.getCy('notifications-button').filter(':visible').click();
			cy.getCy('notification-1').should('be.visible');
		});
	});

	it('Should retain a failed hide for retry and remove only the successful row', () => {
		let calls = 0;
		cy.intercept('PATCH', NotificationPaths.setNotificationVisibilityEndpoint(), (request) => {
			calls++;
			expect(request.body).to.deep.equal({ id: 1, hidden: true });
			request.reply(calls === 1
				? { statusCode: 500, body: generateFailedResultDTO({ statusCode: 500 }) }
				: { statusCode: 200, body: { isSuccess: true, statusCode: 200, errors: [], successes: [] } });
		}).as('hide');

		cy.getCy('notification-1').click();
		cy.wait('@hide').its('response.statusCode').should('eq', 500);
		cy.getCy('api-error').should('contain.text', 'HTTP 500');
		cy.getCy('close-alert-dialog').click();
		cy.getCy('notification-1').should('be.visible');
		cy.getCy('notification-2').should('be.visible');
		cy.getCy('notification-1').click();
		cy.wait('@hide').its('response.statusCode').should('eq', 200);
		cy.getCy('notification-1').should('not.exist');
		cy.getCy('notification-2').should('be.visible');
		cy.then(() => expect(calls).to.equal(2));
	});

	it('Should keep failed clear open and close the drawer only after successful retry', () => {
		let calls = 0;
		cy.intercept('DELETE', NotificationPaths.clearAllNotificationsEndpoint(), (request) => {
			calls++;
			request.reply(calls === 1
				? { statusCode: 500, body: generateFailedResultDTO({ statusCode: 500 }) }
				: { statusCode: 200, body: generateResultDTO({ count: 2 }) });
		}).as('clear');

		cy.getCy('clear-notifications').click();
		cy.wait('@clear').its('response.statusCode').should('eq', 500);
		cy.getCy('api-error').should('contain.text', 'HTTP 500');
		cy.getCy('close-alert-dialog').click();
		cy.get('.notification-drawer').should('be.visible');
		cy.getCy('notification-1').should('be.visible');
		cy.getCy('notification-2').should('be.visible');
		cy.getCy('clear-notifications').click();
		cy.wait('@clear').its('response.statusCode').should('eq', 200);
		cy.get('.notification-drawer').should('not.be.visible');
		cy.getCy('notifications-button').filter(':visible').click();
		cy.get('.notification-drawer').should('be.visible');
		cy.getCy('notification-1').should('not.exist');
		cy.getCy('notification-2').should('not.exist');
		cy.getCy('clear-notifications').should('not.exist');
		cy.then(() => expect(calls).to.equal(2));
	});

	it('Should retain history after a failed reconnect and display missed notifications after recovery', () => {
		const missed: NotificationDTO = {
			id: 3, level: NotificationLevel.Information, createdAt: '2026-11-05T00:00:00Z',
			message: 'Notification created while disconnected', hidden: false,
		};
		let calls = 0;
		cy.intercept('GET', NotificationPaths.getAllNotificationsEndpoint(), (request) => {
			request.reply(++calls === 1
				? { statusCode: 500, body: generateFailedResultDTO({ statusCode: 500 }) }
				: { statusCode: 200, body: generateResultDTO([...notifications, missed]) });
		}).as('reconnectHistory');

		cy.hubPublish('notifications', 'FixtureReconnect', null);
		cy.wait('@reconnectHistory').its('response.statusCode').should('eq', 500);
		cy.getCy('alert-dialog').should('be.visible').and('contain.text', 'HTTP 500');
		cy.getCy('close-alert-dialog').click();
		cy.getCy('notification-1').should('be.visible');
		cy.getCy('notification-2').should('be.visible');
		cy.getCy('notification-3').should('not.exist');
		cy.then(() => expect(calls).to.equal(1));

		cy.hubPublish('notifications', 'FixtureReconnect', null);
		cy.wait('@reconnectHistory').its('response.statusCode').should('eq', 200);
		cy.getCy('notification-1').should('be.visible');
		cy.getCy('notification-2').should('be.visible');
		cy.getCy('notification-3').should('be.visible').and('contain.text', missed.message);
		cy.then(() => expect(calls).to.equal(2));
		cy.reload();
		cy.wait('@reconnectHistory');
		cy.getCy('notifications-button').filter(':visible').click();
		cy.getCy('notification-3').should('be.visible').and('contain.text', missed.message);
	});
});
