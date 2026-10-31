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
  FileSystemDTO,
  FolderPathDTO,
} from "./data-contracts";

import { apiCheckPipe, axiosObservable } from "@api/base";
import queryString from "query-string";

export class FolderPath {
  /**
   * No description
   * * @tags Folderpath
   * @name CreateFolderPathEndpoint
   * @request POST:/api/FolderPath
   * @secure
   */
  createFolderPathEndpoint = (
    data: FolderPathDTO | null,
    params: RequestParams = {},
  ) =>
    axiosObservable<FolderPathDTO>({
      url: `/api/FolderPath`,
      method: "POST",
      data: data,
      secure: true,
      type: ContentType.Json,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<FolderPathDTO>);

  /**
   * No description
   * * @tags Folderpath
   * @name GetAllFolderPathsEndpoint
   * @request GET:/api/FolderPath
   * @secure
   */
  getAllFolderPathsEndpoint = (params: RequestParams = {}) =>
    axiosObservable<FolderPathDTO[]>({
      url: `/api/FolderPath`,
      method: "GET",
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<FolderPathDTO[]>);

  /**
   * No description
   * * @tags Folderpath
   * @name UpdateFolderPathEndpoint
   * @request PUT:/api/FolderPath
   * @secure
   */
  updateFolderPathEndpoint = (
    data: FolderPathDTO,
    params: RequestParams = {},
  ) =>
    axiosObservable<FolderPathDTO>({
      url: `/api/FolderPath`,
      method: "PUT",
      data: data,
      secure: true,
      type: ContentType.Json,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<FolderPathDTO>);

  /**
   * No description
   * * @tags Folderpath
   * @name GetFolderPathDirectoryEndpoint
   * @summary Get all the FolderPaths entities in the database
   * @request GET:/api/FolderPath/directory
   * @secure
   */
  getFolderPathDirectoryEndpoint = (
    query?: {
      path?: string;
    },
    params: RequestParams = {},
  ) =>
    axiosObservable<FileSystemDTO>({
      url: `/api/FolderPath/directory`,
      method: "GET",
      params: query,
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<FileSystemDTO>);

  /**
   * No description
   * * @tags Folderpath
   * @name DeleteFolderPathEndpoint
   * @request DELETE:/api/FolderPath/{id}
   * @secure
   */
  deleteFolderPathEndpoint = (id: number, params: RequestParams = {}) =>
    axiosObservable<BaseResultDTO>({
      url: `/api/FolderPath/${id}`,
      method: "DELETE",
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<BaseResultDTO>);
}

export class FolderPathPaths {
  static createFolderPathEndpoint = () =>
    queryString.stringifyUrl({ url: `/api/FolderPath` });

  static getAllFolderPathsEndpoint = () =>
    queryString.stringifyUrl({ url: `/api/FolderPath` });

  static updateFolderPathEndpoint = () =>
    queryString.stringifyUrl({ url: `/api/FolderPath` });

  static getFolderPathDirectoryEndpoint = (query?: { path?: string }) =>
    queryString.stringifyUrl({ url: `/api/FolderPath/directory`, query });

  static deleteFolderPathEndpoint = (id: number) =>
    queryString.stringifyUrl({ url: `/api/FolderPath/${id}` });
}
