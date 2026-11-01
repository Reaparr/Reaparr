/* eslint-disable */
/* tslint:disable */
// @ts-nocheck
/*
 * ---------------------------------------------------------------
 * ## THIS FILE WAS GENERATED VIA SWAGGER-TYPESCRIPT-API        ##
 * ##                                                           ##
 * ## AUTHOR: acacode                                           ##
 * ## SOURCE: https://github.com/acacode/swagger-typescript-api ##
 * ---------------------------------------------------------------
 */

import type { RequestParams } from "./http-client";
import { ContentType } from "./http-client";

import type {
  BaseResultDTO,
  CountResponseDTO,
  CreateDownloadTasksRequest,
  DownloadPreviewContainerDTO,
  DownloadPreviewRequest,
  DownloadSnapshotDTO,
  DownloadTaskCreationReportDTO,
  DownloadTaskDTO,
  DownloadTaskLogDTO,
  DownloadTaskType,
  ListOfGuid,
} from "./data-contracts";

import { apiCheckPipe, axiosObservable } from "@api/base";
import queryString from "query-string";

export class Download {
  /**
   * No description
   * * @tags Download
   * @name GetAllDownloadTasksEndpoint
   * @request GET:/api/Download
   * @secure
   */
  getAllDownloadTasksEndpoint = (params: RequestParams = {}) =>
    axiosObservable<DownloadSnapshotDTO>({
      url: `/api/Download`,
      method: "GET",
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<DownloadSnapshotDTO>);

  /**
   * No description
   * * @tags Download
   * @name ClearCompletedDownloadTasksByDownloadTaskIdEndpoint
   * @request DELETE:/api/Download/clear/tasks
   * @secure
   */
  clearCompletedDownloadTasksByDownloadTaskIdEndpoint = (
    data: ListOfGuid,
    params: RequestParams = {},
  ) =>
    axiosObservable<CountResponseDTO>({
      url: `/api/Download/clear/tasks`,
      method: "DELETE",
      data: data,
      secure: true,
      type: ContentType.Json,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<CountResponseDTO>);

  /**
   * No description
   * * @tags Download
   * @name ClearCompletedDownloadTasksByServerIdEndpoint
   * @request DELETE:/api/Download/clear/{plexServerId}
   * @secure
   */
  clearCompletedDownloadTasksByServerIdEndpoint = (
    plexServerId: number,
    params: RequestParams = {},
  ) =>
    axiosObservable<CountResponseDTO>({
      url: `/api/Download/clear/${plexServerId}`,
      method: "DELETE",
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<CountResponseDTO>);

  /**
   * No description
   * * @tags Download
   * @name CreateDownloadTasksEndpoint
   * @request POST:/api/Download/create
   * @secure
   */
  createDownloadTasksEndpoint = (
    data: CreateDownloadTasksRequest,
    params: RequestParams = {},
  ) =>
    axiosObservable<DownloadTaskCreationReportDTO>({
      url: `/api/Download/create`,
      method: "POST",
      data: data,
      secure: true,
      type: ContentType.Json,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<DownloadTaskCreationReportDTO>);

  /**
   * No description
   * * @tags Download
   * @name DeleteDownloadTaskEndpoint
   * @request DELETE:/api/Download/delete
   * @secure
   */
  deleteDownloadTaskEndpoint = (data: ListOfGuid, params: RequestParams = {}) =>
    axiosObservable<BaseResultDTO>({
      url: `/api/Download/delete`,
      method: "DELETE",
      data: data,
      secure: true,
      type: ContentType.Json,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<BaseResultDTO>);

  /**
   * No description
   * * @tags Download
   * @name GetDownloadTaskByGuidEndpoint
   * @request GET:/api/Download/detail/{downloadTaskGuid}
   * @secure
   */
  getDownloadTaskByGuidEndpoint = (
    downloadTaskGuid: string,
    query?: {
      type?: DownloadTaskType;
    },
    params: RequestParams = {},
  ) =>
    axiosObservable<DownloadTaskDTO>({
      url: `/api/Download/detail/${downloadTaskGuid}`,
      method: "GET",
      params: query,
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<DownloadTaskDTO>);

  /**
   * No description
   * * @tags Download
   * @name DeleteAllDownloadTaskLogsByDownloadTaskIdEndpoint
   * @request DELETE:/api/Download/logs/{downloadTaskGuid}
   * @secure
   */
  deleteAllDownloadTaskLogsByDownloadTaskIdEndpoint = (
    downloadTaskGuid: string,
    query: {
      /** @format int32 */
      plexLibraryId: number;
      /** @format int32 */
      plexServerId: number;
      type: DownloadTaskType;
    },
    params: RequestParams = {},
  ) =>
    axiosObservable<number>({
      url: `/api/Download/logs/${downloadTaskGuid}`,
      method: "DELETE",
      params: query,
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<number>);

  /**
   * No description
   * * @tags Download
   * @name GetDownloadTaskLogsByDownloadTaskIdEndpoint
   * @request GET:/api/Download/logs/{downloadTaskGuid}
   * @secure
   */
  getDownloadTaskLogsByDownloadTaskIdEndpoint = (
    downloadTaskGuid: string,
    query: {
      /** @format int32 */
      plexLibraryId: number;
      /** @format int32 */
      plexServerId: number;
      /** @format int32 */
      sinceId?: number | null;
      /** @format int32 */
      take?: number | null;
      type: DownloadTaskType;
    },
    params: RequestParams = {},
  ) =>
    axiosObservable<DownloadTaskLogDTO[]>({
      url: `/api/Download/logs/${downloadTaskGuid}`,
      method: "GET",
      params: query,
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<DownloadTaskLogDTO[]>);

  /**
   * No description
   * * @tags Download
   * @name PauseDownloadTaskEndpoint
   * @request PUT:/api/Download/pause/{downloadTaskGuid}
   * @secure
   */
  pauseDownloadTaskEndpoint = (
    downloadTaskGuid: string,
    params: RequestParams = {},
  ) =>
    axiosObservable<BaseResultDTO>({
      url: `/api/Download/pause/${downloadTaskGuid}`,
      method: "PUT",
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<BaseResultDTO>);

  /**
   * No description
   * * @tags Download
   * @name GetDownloadPreviewEndpoint
   * @request POST:/api/Download/preview
   * @secure
   */
  getDownloadPreviewEndpoint = (
    data: DownloadPreviewRequest,
    params: RequestParams = {},
  ) =>
    axiosObservable<DownloadPreviewContainerDTO>({
      url: `/api/Download/preview`,
      method: "POST",
      data: data,
      secure: true,
      type: ContentType.Json,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<DownloadPreviewContainerDTO>);

  /**
   * No description
   * * @tags Download
   * @name RestartDownloadTaskEndpoint
   * @request PUT:/api/Download/restart/{downloadTaskGuid}
   * @secure
   */
  restartDownloadTaskEndpoint = (
    downloadTaskGuid: string,
    params: RequestParams = {},
  ) =>
    axiosObservable<BaseResultDTO>({
      url: `/api/Download/restart/${downloadTaskGuid}`,
      method: "PUT",
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<BaseResultDTO>);

  /**
   * No description
   * * @tags Download
   * @name StartDownloadTaskEndpoint
   * @request PUT:/api/Download/start/{downloadTaskGuid}
   * @secure
   */
  startDownloadTaskEndpoint = (
    downloadTaskGuid: string,
    params: RequestParams = {},
  ) =>
    axiosObservable<BaseResultDTO>({
      url: `/api/Download/start/${downloadTaskGuid}`,
      method: "PUT",
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<BaseResultDTO>);

  /**
   * No description
   * * @tags Download
   * @name StopDownloadTaskEndpoint
   * @request PUT:/api/Download/stop/{downloadTaskGuid}
   * @secure
   */
  stopDownloadTaskEndpoint = (
    downloadTaskGuid: string,
    params: RequestParams = {},
  ) =>
    axiosObservable<BaseResultDTO>({
      url: `/api/Download/stop/${downloadTaskGuid}`,
      method: "PUT",
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<BaseResultDTO>);
}

export class DownloadPaths {
  static getAllDownloadTasksEndpoint = () =>
    queryString.stringifyUrl({ url: `/api/Download` });

  static clearCompletedDownloadTasksByDownloadTaskIdEndpoint = () =>
    queryString.stringifyUrl({ url: `/api/Download/clear/tasks` });

  static clearCompletedDownloadTasksByServerIdEndpoint = (
    plexServerId: number,
  ) => queryString.stringifyUrl({ url: `/api/Download/clear/${plexServerId}` });

  static createDownloadTasksEndpoint = () =>
    queryString.stringifyUrl({ url: `/api/Download/create` });

  static deleteDownloadTaskEndpoint = () =>
    queryString.stringifyUrl({ url: `/api/Download/delete` });

  static getDownloadTaskByGuidEndpoint = (
    downloadTaskGuid: string,
    query?: {
      type?: DownloadTaskType;
    },
  ) =>
    queryString.stringifyUrl({
      url: `/api/Download/detail/${downloadTaskGuid}`,
      query,
    });

  static deleteAllDownloadTaskLogsByDownloadTaskIdEndpoint = (
    downloadTaskGuid: string,
    query: {
      /** @format int32 */
      plexLibraryId: number;
      /** @format int32 */
      plexServerId: number;
      type: DownloadTaskType;
    },
  ) =>
    queryString.stringifyUrl({
      url: `/api/Download/logs/${downloadTaskGuid}`,
      query,
    });

  static getDownloadTaskLogsByDownloadTaskIdEndpoint = (
    downloadTaskGuid: string,
    query: {
      /** @format int32 */
      plexLibraryId: number;
      /** @format int32 */
      plexServerId: number;
      /** @format int32 */
      sinceId?: number | null;
      /** @format int32 */
      take?: number | null;
      type: DownloadTaskType;
    },
  ) =>
    queryString.stringifyUrl({
      url: `/api/Download/logs/${downloadTaskGuid}`,
      query,
    });

  static pauseDownloadTaskEndpoint = (downloadTaskGuid: string) =>
    queryString.stringifyUrl({
      url: `/api/Download/pause/${downloadTaskGuid}`,
    });

  static getDownloadPreviewEndpoint = () =>
    queryString.stringifyUrl({ url: `/api/Download/preview` });

  static restartDownloadTaskEndpoint = (downloadTaskGuid: string) =>
    queryString.stringifyUrl({
      url: `/api/Download/restart/${downloadTaskGuid}`,
    });

  static startDownloadTaskEndpoint = (downloadTaskGuid: string) =>
    queryString.stringifyUrl({
      url: `/api/Download/start/${downloadTaskGuid}`,
    });

  static stopDownloadTaskEndpoint = (downloadTaskGuid: string) =>
    queryString.stringifyUrl({ url: `/api/Download/stop/${downloadTaskGuid}` });
}
