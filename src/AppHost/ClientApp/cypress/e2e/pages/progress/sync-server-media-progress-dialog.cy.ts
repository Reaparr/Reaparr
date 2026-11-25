import {
	JobStatus,
	JobTypes,
	type LibrarySyncJobQueueDTO,
	LibrarySyncJobStatus,
	MessageTypes,
	PlexMediaType,
} from '@dto';
import { generateLibrarySyncProgress, generateLibrarySyncJobQueue, generateLibrarySyncProgressItem } from '@factories';
import { generateResultDTO } from '@mock';
import { PlexLibraryPaths } from '@api/api-paths';

describe('SyncServerMediaDialog', () => {
	const SERVER_COUNT = 3;

	beforeEach(() => {
		cy.basePageSetup({
			plexAccountCount: 1,
			plexServerCount: SERVER_COUNT,
		});

		// Initial queue is empty; job events will request authoritative snapshots.
		cy.intercept('GET', PlexLibraryPaths.getLibrarySyncStatusEndpoint(), {
			statusCode: 200,
			body: generateResultDTO([]),
		});

		cy.visitEmptyPage();
	});

	it('Should display sync progress for multiple servers, each syncing one library at a time', () => {
		cy.getPageData().then((data) => {
			// Select one library from each server to sync in parallel
			const librariesToSync = data.plexServers
				.map((server) => {
					const serverLibraries = data.plexLibraries.filter((lib) => lib.plexServerId === server.id);
					return { server, library: serverLibraries[0]! };
				})
				.filter(({ library }) => library !== undefined);

			// === PHASE 1: Start sync jobs for all servers ===
			let queues: LibrarySyncJobQueueDTO[] = librariesToSync.map(({ server, library }) =>
				generateLibrarySyncJobQueue({
					plexLibraryId: library.id,
					plexServerId: server.id,
					status: LibrarySyncJobStatus.Processing,
				}),
			);
			cy.intercept('GET', PlexLibraryPaths.getLibrarySyncStatusEndpoint(), (request) => {
				request.reply({ statusCode: 200, body: generateResultDTO(queues) });
			}).as('syncSnapshot');
			librariesToSync.forEach(({ server, library }) => {
				cy.hubPublishJobStatusUpdate(JobTypes.LibrarySyncJob, JobStatus.Started, {
					serverId: server.id,
					libraryId: library.id,
					forceMediaRefresh: false,
				});
			});
			cy.wait('@syncSnapshot');

			// Open the sync dialog
			cy.getCy('background-activity-button').click();
			cy.getCy(JobTypes.LibrarySyncJob + 'activity-button').click();
			cy.getCy('sync-server-media-dialog').should('be.visible');

			// === PHASE 2: Verify all servers and libraries are displayed ===
			cy.getCy('sync-server-media-dialog-server-title')
				.should('have.length', SERVER_COUNT)
				.each(($el, index) => {
					// Verify server names are displayed
					cy.wrap($el).should('contain.text', librariesToSync[index]!.server.name);
				});

			cy.getCy('sync-server-media-dialog-library-title')
				.should('have.length', SERVER_COUNT)
				.each(($el, index) => {
					// Verify library titles are displayed
					cy.wrap($el).should('contain.text', librariesToSync[index]!.library.title);
				});

			// === PHASE 3: Simulate progress updates from 0% to 100% ===
			// Send initial 0% progress
			librariesToSync.forEach(({ library }) => {
				cy.hubPublish('progress', MessageTypes.LibraryProgress, generateLibrarySyncProgress({
					type: PlexMediaType.Movie,
					libraryId: library.id,
					received: 0,
					total: 1000,
					items: [generateLibrarySyncProgressItem(PlexMediaType.Movie, { received: 0, total: 1000 })],
				}));
			});

			// Verify progress bars exist for each library
			cy.getCy('sync-server-media-dialog-library-title').each(($el) => {
				cy.wrap($el)
					.parents('.q-tree__node-header')
					.find('.q-linear-progress')
					.should('exist');
			});

			// Progress through 25%, 50%, 75%, 100%
			[250, 500, 750, 1000].forEach((received) => {
				cy.wait(300).then(() => {
					librariesToSync.forEach(({ library }) => {
						cy.hubPublish('progress', MessageTypes.LibraryProgress, generateLibrarySyncProgress({
							type: PlexMediaType.Movie,
							libraryId: library.id,
							received,
							total: 1000,
							items: [generateLibrarySyncProgressItem(PlexMediaType.Movie, { received, total: 1000 })],
						}));
					});
				});
			});

			// === PHASE 4: Complete all sync jobs ===
			cy.then(() => {
				queues = queues.map((queue) => ({
					...queue,
					status: LibrarySyncJobStatus.Completed,
					completedAt: new Date().toISOString(),
				}));
			});
			librariesToSync.forEach(({ server, library }) => {
				cy.hubPublishJobStatusUpdate(JobTypes.LibrarySyncJob, JobStatus.Completed, {
					serverId: server.id,
					libraryId: library.id,
					forceMediaRefresh: false,
				});
			});
			cy.getCy('sync-server-media-dialog-library-status').should('have.length', SERVER_COUNT)
				.each(($status) => cy.wrap($status).should('contain.text', 'Completed'));

			// === PHASE 5: Close dialog ===
			cy.getCy('sync-server-media-dialog-hide-btn').click();
			cy.getCy('sync-server-media-dialog').should('not.exist');
		});
	});

	it('removes a confirmed cancellation before the queue reload and keeps other syncs visible', () => {
		cy.getPageData().then(({ plexServers, plexLibraries }) => {
			const [targetServer, otherServer] = plexServers;
			const target = plexLibraries.find((library) => library.plexServerId === targetServer!.id)!;
			const other = plexLibraries.find((library) => library.plexServerId === otherServer!.id)!;
			let queues = [target, other].map((library) => generateLibrarySyncJobQueue({
				plexLibraryId: library.id,
				plexServerId: library.plexServerId,
				status: LibrarySyncJobStatus.Queued,
			}));
			let releaseSnapshot!: () => void;
			const waitForRemoval = new Cypress.Promise<void>((resolve) => { releaseSnapshot = resolve; });
			let cancelled = false;
			cy.intercept('GET', PlexLibraryPaths.getLibrarySyncStatusEndpoint(), (request) => {
				if (cancelled) {
					return waitForRemoval.then(() => request.reply(200, generateResultDTO(queues)));
				}
				request.reply(200, generateResultDTO(queues));
			}).as('syncSnapshot');
			cy.intercept('DELETE', PlexLibraryPaths.cancelLibrarySyncEndpoint(target.id), (request) => {
				cancelled = true;
				queues = queues.map((queue) => queue.plexLibraryId === target.id
					? { ...queue, status: LibrarySyncJobStatus.Cancelled, completedAt: new Date().toISOString() }
					: queue);
				request.reply(200, generateResultDTO(undefined));
			}).as('cancelSync');

			cy.hubPublishJobStatusUpdate(JobTypes.LibrarySyncJob, JobStatus.Started, { serverId: target.plexServerId, libraryId: target.id });
			cy.wait('@syncSnapshot');
			cy.getCy('background-activity-button').click();
			cy.getCy(JobTypes.LibrarySyncJob + 'activity-button').click();
			cy.getCy('sync-server-media-dialog-library-title').should('have.length', 2);
			cy.getCy(`sync-server-media-dialog-cancel-library-${target.id}`).click();
			cy.wait('@cancelSync').its('response.statusCode').should('eq', 200);
			cy.getCy('sync-server-media-dialog-server-title').should('have.length', 1).and('contain.text', otherServer!.name);
			cy.getCy('sync-server-media-dialog-library-title').should('have.length', 1).and('contain.text', other.title);
			cy.then(() => releaseSnapshot());
			cy.wait('@syncSnapshot');
			cy.getCy('sync-server-media-dialog-server-title').should('have.length', 1).and('contain.text', otherServer!.name);
			cy.getCy('sync-server-media-dialog-library-title').should('have.length', 1).and('contain.text', other.title);
		});
	});
});
