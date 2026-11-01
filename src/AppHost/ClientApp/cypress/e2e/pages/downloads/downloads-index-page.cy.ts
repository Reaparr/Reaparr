import { cloneDeep } from 'lodash-es';
import prettyBytes from 'pretty-bytes';
import { HubConnectionState } from '@microsoft/signalr';
import { route } from '@fixtures';
import { DownloadStatus, MessageTypes, RefreshDataType, type DownloadProgressDTO } from '@dto';
import { generateFailedResultDTO, generateResultDTO, toDownloadPatchMessagePackTuple } from '@mock';

const getVisibleCy = (selector: string) => cy.getCy(selector).filter(':visible').first();

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

describe('Downloads page', () => {
	beforeEach(() => {
		cy.hubClear();
		// Publish to the current document after Cypress navigation or another spec's reload.
		Cypress.Commands.overwrite('hubPublish', (_original, hubName, messageType, payload) =>
			cy.window().then((autWindow) => {
				const hub = autWindow['cypress-signalr-mock']?.mocks.find((mock) => mock.name === hubName);
				if (!hub) {
					throw new Error(`SignalR mock hub ${hubName} is not ready in the current page`);
				}
				hub.publish(messageType, payload);
			}),
		);
	});

	it('Should refresh downloads once and display the new status after a refresh notification', () => {
		cy.basePageSetup({
			seed: 47020,
			plexAccountCount: 1,
			plexServerCount: 1,
			plexMovieLibraryCount: 1,
			movieDownloadTask: 1,
			isLoggedIn: true,
		}).then((data) => {
			const initialTask = data.serverDownloadProgress[0]!.downloads[0]!;
			const refreshedDownloads = cloneDeep(data.serverDownloadProgress);
			const refreshedStatus = initialTask.status === DownloadStatus.Completed ? DownloadStatus.Paused : DownloadStatus.Completed;
			refreshedDownloads[0]!.downloads[0]!.status = refreshedStatus;
			cy.visit(route('/downloads'));
			getVisibleCy(`column-status-${initialTask.id}`).should('have.text', initialTask.status);
			let refreshRequests = 0;
			cy.intercept({ method: 'GET', pathname: '/api/Download' }, (request) => {
				refreshRequests++;
				request.reply({ statusCode: 200, body: generateResultDTO({ downloads: refreshedDownloads, patchSequenceByServer: {} }) });
			}).as('refreshDownloads');

			cy.clock();
			cy.hubPublish('notifications', MessageTypes.RefreshNotification, RefreshDataType.DownloadTasks);
			cy.tick(251);
			cy.wait('@refreshDownloads');
			getVisibleCy(`column-status-${initialTask.id}`).should('have.text', refreshedStatus);
			cy.then(() => expect(refreshRequests).to.equal(1));
		});
	});

	it('Should keep snapshot status and controls ahead of late patches while accepting newer patches', () => {
		cy.basePageSetup({
			seed: 47021,
			plexAccountCount: 1,
			plexServerCount: 1,
			plexMovieLibraryCount: 1,
			movieDownloadTask: 1,
			isLoggedIn: true,
		}).then((data) => {
			const initialDownloads = cloneDeep(data.serverDownloadProgress);
			const initialTask = initialDownloads[0]!.downloads[0]!;
			initialTask.status = DownloadStatus.Queued;
			const completedDownloads = cloneDeep(initialDownloads);
			const completedTask = completedDownloads[0]!.downloads[0]!;
			completedTask.status = DownloadStatus.Completed;
			completedTask.percentage = 100;
			completedTask.dataReceived = completedTask.dataTotal;
			const held = Promise.withResolvers<null>();
			let requests = 0;
			cy.intercept({ method: 'GET', pathname: '/api/Download' }, (request) => {
				if (++requests === 2) {
					return held.promise.then(() => request.reply({
						statusCode: 200,
						body: generateResultDTO({
							downloads: completedDownloads,
							patchSequenceByServer: { [completedDownloads[0]!.id]: 10 },
						}),
					}));
				}
				request.reply({
					statusCode: 200,
					body: generateResultDTO({ downloads: initialDownloads, patchSequenceByServer: {} }),
				});
			}).as('snapshot');

			cy.visit(route('/downloads'));
			cy.wait('@snapshot');
			getVisibleCy(`column-status-${initialTask.id}`).should('have.text', DownloadStatus.Queued);
			cy.clock();
			cy.hubPublish('notifications', MessageTypes.RefreshNotification, RefreshDataType.DownloadTasks);
			cy.tick(251);
			cy.wrap(null).should(() => expect(requests).to.equal(2));

			const publishPatch = (sequence: number, status: DownloadStatus) => cy.hubPublish('download', MessageTypes.DownloadPatch, toDownloadPatchMessagePackTuple({
				serverId: completedDownloads[0]!.id,
				sequence,
				deletedIds: [],
				upserts: [{
					id: initialTask.id, parentId: '', status, percentage: status === DownloadStatus.Completed ? 100 : 50,
					dataReceived: status === DownloadStatus.Completed ? completedTask.dataTotal : completedTask.dataTotal / 2,
					dataTotal: completedTask.dataTotal, downloadSpeed: status === DownloadStatus.Downloading ? 1024 : 0, timeRemaining: 0,
				}],
			}));

			publishPatch(10, DownloadStatus.Downloading);
			cy.then(() => held.resolve(null));
			cy.wait('@snapshot');
			getVisibleCy(`column-status-${initialTask.id}`).should('have.text', DownloadStatus.Completed);
			getVisibleCy(`column-actions-clear-${initialTask.id}`).should('exist');
			getVisibleCy(`column-actions-restart-${initialTask.id}`).should('exist');

			publishPatch(10, DownloadStatus.Paused);
			getVisibleCy(`column-status-${initialTask.id}`).should('have.text', DownloadStatus.Completed);
			getVisibleCy(`column-actions-clear-${initialTask.id}`).should('exist');
			getVisibleCy(`column-actions-restart-${initialTask.id}`).should('exist');

			publishPatch(11, DownloadStatus.Downloading);
			getVisibleCy(`column-status-${initialTask.id}`).should('have.text', DownloadStatus.Downloading);
			getVisibleCy(`column-actions-pause-${initialTask.id}`).should('exist');
			getVisibleCy(`column-actions-stop-${initialTask.id}`).should('exist');
		});
	});

	it('Should update the download task row when the download process is updated', () => {
		cy.basePageSetup({
			plexAccountCount: 1,
			plexServerCount: 1,
			plexMovieLibraryCount: 5,
			movieDownloadTask: 3,
		});

		cy.visit(route('/downloads'));
		cy.url().should('eq', route('/downloads'));

		cy.getPageData().then((data) => {
			const downloadTasks = data.serverDownloadProgress[0]!.downloads;
			Cypress._.times(downloadTasks.length, (downloadTaskIndex) => {
				const iterations = 10;
				Cypress._.times(iterations + 1, (i) => {
					const updatedProgress = cloneDeep(data.serverDownloadProgress[0]!);
					const downloadTask = updatedProgress.downloads[downloadTaskIndex];
					if (!downloadTask) {
						return;
					}
					const dataReceived = i * (downloadTask.dataTotal / iterations);
					const percentage = i * 10;
					const status = percentage === 100
						? DownloadStatus.Completed
						: percentage === 0
							? DownloadStatus.Queued
							: DownloadStatus.Downloading;
					const downloadSpeed = status === DownloadStatus.Queued ? 0 : downloadTask.dataTotal / iterations;
					const timeRemaining = status === DownloadStatus.Queued ? 0 : iterations - i;
					updatedProgress.downloads = [
						{
							...downloadTask,
							percentage,
							status,
							timeRemaining,
							dataReceived,
							downloadSpeed,
						},
					];
					cy.hubPublish('download', MessageTypes.ServerDownloadProgress, updatedProgress);
					getVisibleCy(`column-status-${downloadTask.id}`).should('have.text', status);
					getVisibleCy(`column-dataReceived-${downloadTask.id}`).should('have.text', dataReceived === 0 ? '-' : prettyBytes(dataReceived));
					getVisibleCy(`column-dataTotal-${downloadTask.id}`).should('have.text', prettyBytes(downloadTask.dataTotal));
					getVisibleCy(`column-downloadSpeed-${downloadTask.id}`).should(
						'have.text',
						status === DownloadStatus.Queued ? '-' : prettyBytes(downloadSpeed) + `/s`,
					);
					getVisibleCy(`column-percentage-${downloadTask.id}`).should('have.text', `${percentage}%`);
					getVisibleCy(`column-actions-details-${downloadTask.id}`).should('exist');

					if (status == DownloadStatus.Downloading) {
						getVisibleCy(`column-actions-pause-${downloadTask.id}`).should('exist');
						getVisibleCy(`column-actions-stop-${downloadTask.id}`).should('exist');
					}

					if (status == DownloadStatus.Completed) {
						getVisibleCy(`column-actions-clear-${downloadTask.id}`).should('exist');
						getVisibleCy(`column-actions-restart-${downloadTask.id}`).should('exist');
					}
					// Format timeRemaining as MM:SS
					const minutes = Math.floor(timeRemaining / 60).toString().padStart(2, '0');
					const seconds = (timeRemaining % 60).toString().padStart(2, '0');
					const formattedTimeRemaining = status === DownloadStatus.Queued
						? '-'
						: timeRemaining > 0
							? `${minutes}:${seconds}`
							: '-';

					getVisibleCy(`column-timeRemaining-${downloadTask.id}`).should(
						'have.text',
						formattedTimeRemaining,
					);
				});
			});
		});
	});

	it('Should open details dialog when clicking on the details action button next to a download task row', () => {
		cy.basePageSetup({
			plexAccountCount: 1,
			plexServerCount: 1,
			plexMovieLibraryCount: 5,
			movieDownloadTask: 5,
			setDownloadDetails: true,
		});
		cy.visit(route('/downloads'));
		cy.url().should('eq', route('/downloads'));
		cy.getPageData().then((data) => {
			const downloadTask = data.detailDownloadTasks[0]!;
			cy.intercept({
				method: 'GET',
				pathname: `/api/Download/logs/${downloadTask.id}`,
			}, generateResultDTO([])).as('downloadTaskLogs');
			getVisibleCy(`column-actions-details-${downloadTask.id}`).click();
			cy.wait('@downloadTaskLogs');
			cy.getCy('download-details-dialog-status').should('contain.text', downloadTask.status);
			cy.getCy('download-details-dialog-file-name').should('contain.text', downloadTask.fileName);
			cy.getCy('download-details-dialog-download-path').should('contain.text', downloadTask.downloadDirectory);
			cy.getCy('download-details-dialog-destination-path').should('contain.text', downloadTask.destinationDirectory);
			cy.getCy('download-details-dialog-download-url').should('contain.text', downloadTask.downloadUrl);
		});
	});
	it('Should remove completed rows cleared elsewhere while retaining active rows after notification', () => {
		cy.basePageSetup({
			seed: 47095, plexAccountCount: 1, plexServerCount: 1, plexMovieLibraryCount: 1, movieDownloadTask: 2,
		}).then((data) => {
			const queue = cloneDeep(data.serverDownloadProgress);
			const removed = queue[0]!.downloads[0]!;
			const active = queue[0]!.downloads[1]!;
			removed.status = DownloadStatus.Completed;
			active.status = DownloadStatus.Queued;
			cy.intercept({ method: 'GET', pathname: '/api/Download' }, (request) => {
				request.reply({ statusCode: 200, body: generateResultDTO({ downloads: queue, patchSequenceByServer: {} }) });
			}).as('recipientQueue');
			cy.visit(route('/downloads'));
			cy.getCy(`column-status-${removed.id}`).first().scrollIntoView().should('have.text', DownloadStatus.Completed);
			cy.getCy(`column-status-${active.id}`).first().scrollIntoView().should('have.text', DownloadStatus.Queued);

			// Another client completes the clear; this browser receives only its notification.
			cy.then(() => {
				queue[0]!.downloads = [active];
			});
			cy.hubPublish('notifications', MessageTypes.RefreshNotification, RefreshDataType.DownloadTasks);
			cy.wait('@recipientQueue');
			cy.getCy(`column-title-${removed.id}`).should('not.exist');
			cy.getCy(`column-status-${active.id}`).first().scrollIntoView().should('have.text', DownloadStatus.Queued);
			cy.reload();
			cy.wait('@recipientQueue');
			cy.getCy(`column-title-${removed.id}`).should('not.exist');
			cy.getCy(`column-status-${active.id}`).first().scrollIntoView().should('have.text', DownloadStatus.Queued);
		});
	});
	it('Should retain failed clear and delete actions and recover through retry and reload', () => {
		cy.viewport(1280, 800);
		cy.basePageSetup({
			seed: 47094, plexAccountCount: 1, plexServerCount: 1, plexMovieLibraryCount: 1, movieDownloadTask: 2,
		}).then((data) => {
			const queue = cloneDeep(data.serverDownloadProgress);
			const completed = queue[0]!.downloads[0]!;
			const retained = queue[0]!.downloads[1]!;
			const setStatus = (item: DownloadProgressDTO, status: DownloadStatus) => {
				item.status = status;
				for (const child of item.children ?? []) setStatus(child, status);
			};
			setStatus(completed, DownloadStatus.Completed);
			setStatus(retained, DownloadStatus.Stopped);
			cy.intercept({ method: 'GET', pathname: '/api/Download' }, (request) => {
				request.reply({ statusCode: 200, body: generateResultDTO({ downloads: queue, patchSequenceByServer: {} }) });
			}).as('queue');
			const failure = generateFailedResultDTO({
				statusCode: 503, errors: [{ message: 'Download mutation unavailable', reasons: [], metadata: {} }],
			});
			let clearAttempts = 0;
			cy.intercept('DELETE', `**/api/Download/clear/${queue[0]!.id}`, (request) => {
				if (++clearAttempts === 1) {
					request.reply({ statusCode: 503, body: failure });
					return;
				}
				queue[0]!.downloads = [retained];
				request.reply({ statusCode: 200, body: generateResultDTO({ count: 1 }) });
			}).as('clear');
			let deleteAttempts = 0;
			cy.intercept('DELETE', '**/api/Download/delete', (request) => {
				expect(request.body).to.deep.equal([retained.id]);
				if (++deleteAttempts === 1) {
					request.reply({ statusCode: 503, body: failure });
					return;
				}
				queue[0]!.downloads = [];
				request.reply({ statusCode: 200, body: generateResultDTO(null) });
			}).as('delete');

			cy.visit(route('/downloads'));
			getVisibleCy(`column-status-${completed.id}`).should('have.text', DownloadStatus.Completed);
			cy.getCy('clear-completed-by-server-button').first().scrollIntoView().should('be.visible').click();
			cy.getCy('confirmation-dialog-confirmation-button').click();
			cy.wait('@clear').its('response.statusCode').should('equal', 503);
			cy.getCy('alert-dialog').should('contain.text', 'Download mutation unavailable');
			cy.getCy('close-alert-dialog').click();
			cy.getCy('confirmation-dialog').should('be.visible');
			cy.getCy(`column-title-${completed.id}`).should('exist');
			cy.getCy('confirmation-dialog-confirmation-button').should('not.be.disabled').click();
			cy.wait('@clear').its('response.statusCode').should('equal', 200);
			cy.getCy('confirmation-dialog').should('not.exist');
			cy.getCy(`column-title-${completed.id}`).should('not.exist');
			cy.getCy(`column-title-${retained.id}`).first().scrollIntoView().should('be.visible').and('have.text', retained.title);

			cy.getCy(`column-actions-delete-${retained.id}`).first().scrollIntoView().should('be.visible').click();
			cy.wait('@delete').its('response.statusCode').should('equal', 503);
			cy.getCy('alert-dialog').should('contain.text', 'Download mutation unavailable');
			cy.getCy('close-alert-dialog').click();
			cy.getCy(`column-title-${retained.id}`).first().scrollIntoView().should('be.visible').and('have.text', retained.title);
			cy.getCy(`column-actions-delete-${retained.id}`).first().scrollIntoView().should('not.be.disabled').click();
			cy.wait('@delete').its('response.statusCode').should('equal', 200);
			cy.getCy(`column-title-${retained.id}`).should('not.exist');
			cy.reload();
			cy.wait('@queue');
			cy.getCy(`column-title-${completed.id}`).should('not.exist');
			cy.getCy(`column-title-${retained.id}`).should('not.exist');
		});
	});
	it('Should recover missed queue changes without overwriting a reconnect patch and still clear completed rows', () => {
		cy.basePageSetup({
			seed: 47098, plexAccountCount: 1, plexServerCount: 1, plexMovieLibraryCount: 1, movieDownloadTask: 2,
		}).then((data) => {
			const queue = cloneDeep(data.serverDownloadProgress);
			const current = queue[0]!.downloads[0]!;
			const removed = queue[0]!.downloads[1]!;
			for (const task of queue[0]!.downloads) {
				task.children = [];
				task.status = DownloadStatus.Stopped;
			}
			const held = Promise.withResolvers<null>();
			let requests = 0;
			cy.intercept({ method: 'GET', pathname: '/api/Download' }, (request) => {
				const response = generateResultDTO({ downloads: cloneDeep(queue), patchSequenceByServer: {} });
				if (++requests === 2) return held.promise.then(() => request.reply({ statusCode: 200, body: response }));
				request.reply({ statusCode: 200, body: response });
			}).as('recoveryQueue');
			cy.visit(route('/downloads'), { onBeforeLoad: installReconnectFixture });
			cy.wait('@recoveryQueue');
			cy.getCy(`column-status-${current.id}`).first().should('have.text', DownloadStatus.Stopped);
			const patch = {
				serverId: queue[0]!.id, sequence: 99, deletedIds: [], upserts: [{
					id: current.id, parentId: '', status: DownloadStatus.Downloading, percentage: 75,
					dataReceived: 75, dataTotal: 100, downloadSpeed: 0, timeRemaining: 0,
				}],
			};
			cy.hubPublish('download', MessageTypes.DownloadPatch, toDownloadPatchMessagePackTuple(patch));
			cy.getCy(`column-status-${current.id}`).first().should('have.text', DownloadStatus.Downloading);
			cy.then(() => {
				queue[0]!.downloads = [current];
				current.status = DownloadStatus.Paused;
			});
			cy.hubPublish('download', 'FixtureReconnect', null);
			cy.wrap(null).should(() => expect(requests).to.equal(2));
			cy.then(() => {
				patch.sequence = 1;
				patch.upserts[0]!.status = DownloadStatus.Completed;
				return cy.hubPublish('download', MessageTypes.DownloadPatch, toDownloadPatchMessagePackTuple(patch));
			});
			cy.getCy(`column-status-${current.id}`).first().should('have.text', DownloadStatus.Completed);
			cy.then(() => held.resolve(null));
			cy.wait('@recoveryQueue');
			cy.getCy(`column-title-${removed.id}`).should('not.exist');
			cy.getCy(`column-status-${current.id}`).first().should('have.text', DownloadStatus.Completed);
			cy.intercept('DELETE', `**/api/Download/clear/${queue[0]!.id}`, (request) => {
				queue[0]!.downloads = [];
				request.reply({ statusCode: 200, body: generateResultDTO({ count: 1 }) });
			}).as('clearRecoveredQueue');
			cy.getCy('clear-completed-by-server-button').first().scrollIntoView().click();
			cy.getCy('confirmation-dialog-confirmation-button').click();
			cy.wait('@clearRecoveredQueue');
			cy.hubPublish('notifications', MessageTypes.RefreshNotification, RefreshDataType.DownloadTasks);
			cy.getCy('confirmation-dialog').should('not.exist');
			cy.getCy(`column-title-${current.id}`).should('not.exist');
			cy.reload();
			cy.wait('@recoveryQueue');
			cy.getCy(`column-title-${current.id}`).should('not.exist');
		});
	});
});
