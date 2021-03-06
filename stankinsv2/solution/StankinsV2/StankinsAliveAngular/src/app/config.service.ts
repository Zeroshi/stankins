import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { switchMap, tap, map } from 'rxjs/operators';
import { ResultTypeStankins } from "./whatsimple/ResultTypeStankins";
const httpOptionsPlain = {
  headers: new HttpHeaders({
    'Content-Type':  'text/plain'
  })
};
@Injectable({
  providedIn: 'root'
})
export class ConfigService {

  constructor(private http: HttpClient) {

  }
  public GetConfig(id: string): Observable<Configuration> {

      const url = '/api/values/' + id;
      // window.alert(url);
      return this.http.get<Configuration>(url, httpOptionsPlain);

      // return of(new Configuration());
  }
  public GetStankinsAll(): Observable<ResultTypeStankins[]> {
    const url = '/api/whatexecutesimple/';
    return this.http.get<ResultTypeStankins[]>(url);

  }
}

export class RowConfiguration {

  constructor(props: string[]) {
    const self = this;
    props.forEach(it => self[it] = null);
  }



}
export class Configuration {
  public UserName: string;
  public ExecutorsDynamic: Executor[];
  public static friendlyName(typeAlive: string) {
    switch ( typeAlive) {
      case 'StankinsStatusWeb.StartProcess, StankinsAliveMonitor':
      case 'Stankins.Alive.ReceiverProcessAlive':
        return 'Process';
      case 'StankinsStatusWeb.PingAddress, StankinsAliveMonitor':
      case 'Stankins.Alive.ReceiverPing':
        return 'Ping';
      case 'StankinsStatusWeb.WebAdress, StankinsAliveMonitor':
      case 'Stankins.Alive.ReceiverWeb':
        return 'Web';
      case 'StankinsStatusWeb.DatabaseConnection, StankinsAliveMonitor':
      case 'Stankins.Alive.ReceiverDBSqlServer':
        return 'Database';
      default:
        return '!!' + typeAlive;
    }
  }
  public static TransformToRow(execs: Array<Executor>): [string[], RowConfiguration[]] {

    const types = execs.map(it => it.Data.Type).filter((value, index, self) => {
      return self.indexOf(value) === index;
    });
    const ret: RowConfiguration[] = [];
    // window.alert(types);
    execs.forEach(it => {

        let foundType = false;
        const type = it.Data.Type;
        ret.forEach( r => {
          const exist = (r[type] != null) ; // (JSON.stringify(r[type]).length > 0);
          if (!exist) {
            r[type] = it;
            foundType = true;
            return;
          }
        });
        if (!foundType) {
          const r = new RowConfiguration(types);
          r[type] = it;
          ret.push(r);
        }
    });
    // const r = new RowConfiguration(types);
    // ret.push(r);
    // window.alert(ret.length );
    // ret.forEach( r => {
    //   window.alert(JSON.stringify(r));
    // });
    return [types, ret];

  }
}
export class Executor {
  public Data: Data;
  public CustomData: CustomData;

}
export  class Data {
  Type: string;
  CRON: string;
}
export class CustomData {
  Name: string;
  Tags: string;
  Icon: string;
}
