import { route } from '@fixtures';
import { JobStatus, JobTypes, LibrarySyncJobStatus } from '@dto';
import { generateLibrarySyncJobQueue } from '@factories';
import { PlexLibraryPaths } from '@api-urls';
import { generateResultDTO } from '@mock';

describe('Check BackgroundActivityToggleButton', () => {
	beforeEach(() => {
		cy.basePageSetup({
			plexAccountCount: 1,
			plexServerCount: 3,
			maxServerConnections: 3,
		});
		cy.visit(route('/empty'));
	});

	it('Should show loading while CheckPlexServerConnectionsJob runs and clear it on completion', function () {
		cy.getPageData().then((data) => {
			cy.getCy('background-activity-button').should('exist').and('be.visible');
			cy.getCy('background-activity-button-badge').should('not.exist');
			cy.getCy('background-activity-button-loading').should('not.exist');

			cy.hubPublishCheckPlexServerConnectionsJob(JobStatus.Started, data.plexServers, data.plexServerConnections);
			cy.getCy('background-activity-button').click();

			cy.getCy('background-activity-button-badge').should('contain.text', 1);
			cy.getCy('background-activity-button-loading').should('be.exist');

			cy.getCy(JobTypes.CheckAllConnectionsStatusByPlexServerJob + 'activity-button').click();

			cy.getCy('check-server-connection-dialog').should('exist').and('be.visible');

			cy.hubPublishCheckPlexServerConnectionsJob(JobStatus.Completed, data.plexServers, data.plexServerConnections);
			cy.getCy('background-activity-button-badge').should('not.exist');
			cy.getCy('background-activity-button-loading').should('not.exist');
		});
	});

	it('Should keep queue-only library sync activity visible from persisted queue rows', function () {
		cy.getPageData().then((data) => {
			const library = data.plexLibraries[0];
			if (!library) {
				throw new Error('Library not found');
			}

			cy.intercept('GET', PlexLibraryPaths.getLibrarySyncStatusEndpoint(), {
				statusCode: 200,
				body: generateResultDTO([generateLibrarySyncJobQueue({
					plexLibraryId: library.id,
					plexServerId: library.plexServerId,
					status: LibrarySyncJobStatus.Queued,
				})]),
			}).as('queuedSyncSnapshot');
			cy.reload();
			cy.wait('@queuedSyncSnapshot');
			cy.getCy('background-activity-button').click();
			cy.getCy('background-activity-button-badge').should('contain.text', 1);
			cy.getCy('background-activity-button-loading').should('exist');
			cy.getCy(JobTypes.LibrarySyncJob + 'activity-button').click();
			cy.getCy('sync-server-media-dialog').should('be.visible');
			cy.getCy('sync-server-media-dialog-library-status')
				.should('contain.text', 'Queued')
				.and('not.contain.text', '%');
			cy.getCy('sync-server-media-dialog-library-eta').should('not.exist');
		});
	});
});
