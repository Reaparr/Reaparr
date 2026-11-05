import { route, headers } from '@fixtures';
import { DownloadPaths } from '@api-urls';
import { generateFailedResultDTO, generateResultDTO } from '@mock';
import { DownloadStatus, NotificationLevel, type DownloadTaskDTO, type DownloadTaskLogDTO } from '@dto';

const visible = (selector: string) => cy.getCy(selector).filter(':visible').first();

function log(id: number, message: string): DownloadTaskLogDTO {
	return { id, message, logLevel: NotificationLevel.Information, status: DownloadStatus.Downloading, createdAt: '2026-11-04T10:00:00Z' };
}

describe('Download details opening ownership', () => {
	let first: DownloadTaskDTO;
	let second: DownloadTaskDTO;
	const pending = new Map<string, () => void>();

	beforeEach(() => {
		cy.basePageSetup({
			seed: 116, isLoggedIn: true, plexAccountCount: 1, plexServerCount: 2,
			plexMovieLibraryCount: 1, movieDownloadTask: 1, setDownloadDetails: true,
		}).then(({ detailDownloadTasks }) => {
			const [firstTask, secondTask] = detailDownloadTasks;
			if (!firstTask || !secondTask) throw new Error('Expected two download tasks');
			first = { ...firstTask, fullTitle: 'First download details' };
			second = { ...secondTask, fullTitle: 'Second download details' };
			cy.intercept('GET', DownloadPaths.getDownloadTaskByGuidEndpoint(first.id), {
				statusCode: 200, body: generateResultDTO(first), ...headers,
			}).as('firstDetails');
			cy.intercept('GET', DownloadPaths.getDownloadTaskByGuidEndpoint(second.id), {
				statusCode: 200, body: generateResultDTO(second), ...headers,
			}).as('secondDetails');
			cy.intercept({ method: 'GET', pathname: `/api/Download/logs/${first.id}` }, {
				statusCode: 200, body: generateResultDTO([log(101, 'First download log')]), ...headers,
			}).as('firstLogs');
			cy.intercept({ method: 'GET', pathname: `/api/Download/logs/${second.id}` }, {
				statusCode: 200, body: generateResultDTO([log(202, 'Second download log')]), ...headers,
			}).as('secondLogs');
			// Keep periodic log polling from repairing a stale initial snapshot during the assertion.
			cy.clock(Date.now(), ['Date', 'setInterval', 'clearInterval']);
			cy.visit(route('/downloads'));
		});
	});

	afterEach(() => {
		pending.forEach((release) => release());
		pending.clear();
	});

	it('Should preserve the current details after a closed request finishes and recover a later failed opening', () => {
		cy.intercept('GET', DownloadPaths.getDownloadTaskByGuidEndpoint(first.id), (request) => {
			const { promise, resolve } = Promise.withResolvers<undefined>();
			pending.set('details', () => {
				pending.delete('details');
				request.reply({ statusCode: 200, body: generateResultDTO(first), ...headers });
				resolve(undefined);
			});
			return promise;
		}).as('staleDetails');

		visible(`column-actions-details-${first.id}`).click();
		cy.wrap(null).should(() => expect(pending.has('details')).to.equal(true));
		cy.get('body').type('{esc}');
		cy.get('.q-dialog').should('not.exist');
		visible(`column-actions-details-${second.id}`).click();
		cy.wait('@secondDetails');
		cy.wait('@secondLogs').its('request.query').should('include', {
			type: second.downloadTaskType, plexServerId: String(second.plexServerId),
			plexLibraryId: String(second.plexLibraryId), take: '50',
		});
		visible('download-details-dialog-title').should('have.text', second.fullTitle);

		cy.then(() => pending.get('details')!());
		cy.wait('@staleDetails');
		visible('download-details-dialog-title').should('have.text', second.fullTitle);
		visible('download-details-dialog-file-name').should('contain.text', second.fileName);
		visible('download-details-dialog-status').should('contain.text', second.status);
		cy.get('@secondLogs.all').should('have.length', 1);
		cy.get('@firstLogs.all').should('have.length', 0);
		visible('dialog-close-button').click();
		cy.get('.q-dialog').should('not.exist');

		cy.intercept({ method: 'GET', pathname: `/api/Download/detail/${second.id}`, times: 1 }, {
			statusCode: 404,
			body: generateFailedResultDTO({ statusCode: 404, errors: [{ message: 'Current task unavailable', metadata: {}, reasons: [] }] }),
			...headers,
		}).as('missingDetails');
		visible(`column-actions-details-${second.id}`).click();
		cy.wait('@missingDetails').its('response.statusCode').should('eq', 404);
		visible('alert-dialog').should('contain.text', 'Current task unavailable');
		visible('close-alert-dialog').click();
		cy.getCy('alert-dialog').should('not.exist');
		cy.get('body').type('{esc}');
		cy.get('.q-dialog').should('not.exist');
		visible(`column-actions-details-${second.id}`).click();
		cy.wait(['@secondDetails', '@secondLogs']);
		visible('download-details-dialog-title').should('have.text', second.fullTitle);
		visible('download-details-dialog-destination-path').should('contain.text', second.destinationDirectory);
		cy.contains('Current task unavailable').should('not.exist');
	});

	it('Should display only the current initial logs when the previous opening returns its logs last', () => {
		cy.intercept({ method: 'GET', pathname: `/api/Download/logs/${first.id}` }, (request) => {
			const { promise, resolve } = Promise.withResolvers<undefined>();
			pending.set('logs', () => {
				pending.delete('logs');
				request.reply({ statusCode: 200, body: generateResultDTO([log(999, 'Wrong first download log')]), ...headers });
				resolve(undefined);
			});
			return promise;
		}).as('staleLogs');

		visible(`column-actions-details-${first.id}`).click();
		cy.wait('@firstDetails');
		visible('download-details-dialog-title').should('have.text', first.fullTitle);
		cy.wrap(null).should(() => expect(pending.has('logs')).to.equal(true));
		visible('dialog-close-button').click();
		cy.get('.q-dialog').should('not.exist');
		visible(`column-actions-details-${second.id}`).click();
		cy.wait(['@secondDetails', '@secondLogs']);
		cy.then(() => pending.get('logs')!());
		cy.wait('@staleLogs');

		visible('download-details-dialog-title').should('have.text', second.fullTitle);
		visible('download-details-dialog-tab-2').click();
		visible('download-details-dialog-tab-content-2').should('contain.text', 'Second download log');
		visible('download-details-dialog-tab-content-2').should('not.contain.text', 'Wrong first download log');
		cy.get('@secondLogs.all').should('have.length', 1);
	});

	it('Should expose named keyboard controls and keep log deletion behind confirmation', () => {
		cy.intercept({ method: 'GET', pathname: `/api/Download/logs/${first.id}` }, {
			statusCode: 200, body: generateResultDTO([log(101, 'Older toolbar log'), log(102, 'Newer toolbar log')]), ...headers,
		}).as('toolbarLogs');
		cy.intercept({ method: 'DELETE', pathname: `/api/Download/logs/${first.id}` }, {
			statusCode: 200, body: generateResultDTO(null), ...headers,
		}).as('deleteLogs');
		visible(`column-actions-details-${first.id}`).click();
		cy.wait(['@firstDetails', '@toolbarLogs']);
		visible('download-details-dialog-tab-2').click();

		cy.findByRole('button', { name: 'Copy all logs' }).should('be.visible');
		cy.get('.logs-panel .log-row').first().should('contain.text', 'Older toolbar log');
		cy.findByRole('button', { name: 'Oldest first' }).focus();
		cy.focused().type('{enter}');
		cy.findByRole('button', { name: 'Newest first' }).should('be.visible');
		cy.get('.logs-panel .log-row').first().should('contain.text', 'Newer toolbar log');

		cy.findByRole('button', { name: 'Filter by log level' }).focus();
		cy.focused().type('{enter}');
		cy.get('.q-menu').filter(':visible').should('be.visible');
		cy.get('body').type('{esc}');
		cy.get('.q-menu').should('not.exist');

		cy.findByRole('button', { name: 'Delete all logs' }).focus();
		cy.focused().type('{enter}');
		visible('confirmation-dialog').should('contain.text', 'Are you sure you want to delete all logs for this download task?');
		cy.get('@deleteLogs.all').should('have.length', 0);
		visible('confirmation-dialog').findByRole('button', { name: 'Cancel' }).focus();
		cy.focused().type('{enter}');
		cy.getCy('confirmation-dialog').should('not.exist');
		cy.get('@deleteLogs.all').should('have.length', 0);
		visible('download-details-dialog-tab-content-2').should('contain.text', 'Newer toolbar log');

		cy.findByRole('button', { name: 'Delete all logs' }).focus();
		cy.focused().type('{enter}');
		visible('confirmation-dialog').findByRole('button', { name: 'Confirm' }).focus();
		cy.focused().type('{enter}');
		cy.wait('@deleteLogs').its('request.query').should('include', {
			type: first.downloadTaskType, plexServerId: String(first.plexServerId),
			plexLibraryId: String(first.plexLibraryId),
		});
		visible('download-details-dialog-tab-content-2').should('contain.text', 'No logs found for this download task.');
	});
});
