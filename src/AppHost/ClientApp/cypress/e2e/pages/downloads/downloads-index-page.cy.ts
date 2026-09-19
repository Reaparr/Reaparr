import { cloneDeep } from 'lodash-es';
import prettyBytes from 'pretty-bytes';
import { route } from '@fixtures';
import { DownloadStatus, MessageTypes } from '@dto';
import { generateResultDTO } from '@mock';

const getVisibleCy = (selector: string) => cy.getCy(selector).filter(':visible').first();

describe('Downloads page', () => {
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
});
