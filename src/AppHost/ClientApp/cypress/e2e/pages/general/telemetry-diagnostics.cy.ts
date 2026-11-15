import { route } from '@fixtures';
import { DebugPaths } from '@api-urls';
import { TelemetryDiagnosticOrigin } from '@dto';
import { generateFailedResultDTO, generateResultDTO } from '@mock';

describe('Telemetry diagnostics', () => {
	beforeEach(() => {
		cy.basePageSetup({ seed: 678, plexAccountCount: 0, plexServerCount: 0 });
	});

	it('Should show independent outcomes, recover on a deliberate retry, and never send on page load', () => {
		const requests: unknown[] = [];
		cy.intercept('POST', `**${DebugPaths.sendTelemetryDiagnosticEndpoint()}`, (request) => {
			requests.push(request.body);
			const failed = requests.length === 1;
			request.reply({
				statusCode: failed ? 503 : 200,
				delay: 300,
				body: failed ? generateFailedResultDTO({ statusCode: 503 }) : generateResultDTO(null),
			});
		}).as('diagnostic');

		cy.visit(route('/debug-pages/telemetry'));
		cy.getCy('telemetry-diagnostic-page').should('be.visible');
		cy.getCy('telemetry-backend-status').should('contain.text', 'Not run yet');
		cy.getCy('telemetry-frontend-status').should('contain.text', 'Not run yet');
		cy.then(() => expect(requests).to.deep.equal([]));

		cy.getCy('telemetry-send-test-event').click().should('be.disabled');
		cy.wait('@diagnostic').its('response.statusCode').should('eq', 503);
		cy.wait('@diagnostic').its('response.statusCode').should('eq', 200);
		cy.getCy('telemetry-backend-status').should('contain.text', 'Diagnostic event was not accepted for sending');
		cy.getCy('telemetry-frontend-status').should('contain.text', 'Diagnostic event accepted for sending');
		cy.getCy('alert-dialog').should('be.visible');
		cy.getCy('close-alert-dialog').click();
		cy.getCy('alert-dialog').should('not.exist');
		cy.getCy('telemetry-send-test-event').should('not.be.disabled').click();
		cy.wait('@diagnostic').its('response.statusCode').should('eq', 200);
		cy.wait('@diagnostic').its('response.statusCode').should('eq', 200);
		cy.getCy('telemetry-backend-status').should('contain.text', 'Diagnostic event accepted for sending');
		cy.getCy('telemetry-frontend-status').should('contain.text', 'Diagnostic event accepted for sending');
		cy.then(() => expect(requests).to.deep.equal([
			{ origin: TelemetryDiagnosticOrigin.Backend },
			{ origin: TelemetryDiagnosticOrigin.Frontend },
			{ origin: TelemetryDiagnosticOrigin.Backend },
			{ origin: TelemetryDiagnosticOrigin.Frontend },
		]));

		cy.reload();
		cy.getCy('telemetry-diagnostic-page').should('be.visible');
		cy.getCy('telemetry-backend-status').should('contain.text', 'Not run yet');
		cy.getCy('telemetry-frontend-status').should('contain.text', 'Not run yet');
		cy.then(() => expect(requests).to.have.length(4));
	});
});
