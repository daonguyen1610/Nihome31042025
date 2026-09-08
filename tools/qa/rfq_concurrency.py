"""Opt-in HTTP concurrency probe for an isolated SQL-backed validation stack.

Creates retained RFQ/contract evidence in the supplied projects. Supply a private
JSON credentials file with procurement and bgd entries containing phoneNumber
and password. Both accounts must have their normal project permissions. Projects
need approved VND BOQs, an active Procurement owner and an active Supplier/Both.
This is a relational integration probe, not a browser test or an InMemory proof.
"""
import argparse
import concurrent.futures
import datetime as dt
import json
import threading
import urllib.error
import urllib.request
import uuid
from pathlib import Path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--base-url', required=True)
    parser.add_argument('--credentials', type=Path, required=True)
    parser.add_argument('--projects', type=int, nargs='+', required=True)
    parser.add_argument('--workers', type=int, default=4)
    parser.add_argument('--rounds', type=int, default=3)
    args = parser.parse_args()
    if not 2 <= args.workers <= 8 or not 1 <= args.rounds <= 10:
        parser.error('Use 2–8 workers and 1–10 rounds.')
    tokens = {}

    def call(role, path, body=None, accepted=(200, 201)):
        headers = {'Accept': 'application/json'}
        if role in tokens:
            headers['Authorization'] = 'Bearer ' + tokens[role]
        if body is not None:
            headers.update({'Content-Type': 'application/json', 'Idempotency-Key': str(uuid.uuid4())})
        request = urllib.request.Request(args.base_url.rstrip('/') + path,
                                         data=json.dumps(body).encode() if body is not None else None,
                                         headers=headers)
        try:
            response = urllib.request.urlopen(request, timeout=45)
        except urllib.error.HTTPError as error:
            response = error
        with response:
            raw = response.read()
            if response.status not in accepted:
                # Do not include response bodies: a login response may contain secrets.
                raise AssertionError(f'{path}: HTTP {response.status}, expected {accepted}')
            return response.status, json.loads(raw) if raw else None

    credentials = json.loads(args.credentials.read_text())
    for role in ('procurement', 'bgd'):
        _, login = call(role, '/api/auth/login', credentials[role])
        tokens[role] = login['accessToken']
    deadline = (dt.datetime.now(dt.timezone.utc) + dt.timedelta(days=7)).isoformat()
    validity = (dt.datetime.now(dt.timezone.utc) + dt.timedelta(days=14)).isoformat()

    def parallel(jobs):
        barrier = threading.Barrier(len(jobs))
        def run(job):
            barrier.wait(timeout=15)
            return job()
        with concurrent.futures.ThreadPoolExecutor(max_workers=len(jobs)) as pool:
            return list(pool.map(run, jobs))

    for round_number in range(args.rounds):
        def create(index):
            project_id = args.projects[index % len(args.projects)]
            base = f'/api/operational-projects/{project_id}/procurement/rfqs'
            _, refs = call('procurement', base + '/references')
            revision = refs['revisions'][0]
            source = next(line for line in revision['lines'] if line['quantity'] >= 1)
            vendor = next(v for v in refs['vendors'] if v['type'] in ('Supplier', 'Both'))
            _, rfq = call('procurement', base, {
                'title': 'Concurrent factory package ' + uuid.uuid4().hex[:8],
                'sourceBoqRevisionId': revision['id'], 'ownerUserId': refs['owners'][0]['id'],
                'dueAt': deadline, 'vendorIds': [vendor['id']],
                'lines': [{'projectBoqLineId': source['id'], 'quantity': 1}],
            })
            return {'path': base + '/' + str(rfq['header']['id']), 'rfq': rfq, 'vendor': vendor['id']}

        packages = parallel([lambda i=i: create(i) for i in range(args.workers)])

        def move(package, action, extra=None, role='procurement'):
            _, package['rfq'] = call(role, package['path'] + '/' + action,
                                     {'rowVersion': package['rfq']['header']['rowVersion'], **(extra or {})})
        # Creation inserts children in EF's write order, while issue reads the
        # existing graph. Exercise their overlap as well as concurrent updates.
        results = parallel([lambda p=p: move(p, 'issue') for p in packages] + [lambda: create(0)])
        extra = results[-1]
        move(extra, 'issue')
        packages.append(extra)
        for price in (110, 100):
            parallel([lambda p=p: move(p, 'bids', {
                'vendorId': p['vendor'], 'leadTimeDays': 7, 'paymentTerms': 'After inspected delivery',
                'validUntil': validity, 'documentIds': [],
                'lines': [{'rfqLineId': p['rfq']['lines'][0]['id'], 'unitPrice': price}],
            }) for p in packages])
        parallel([lambda p=p: move(p, 'evaluate') for p in packages])

        # Two decisions race on one RFQ while other RFQs are also being awarded.
        def award(package):
            rfq = package['rfq']
            bid = next(b for b in rfq['bids'] if b['isCurrent'])
            return call('bgd', package['path'] + '/award', {
                'rowVersion': rfq['header']['rowVersion'], 'bidId': bid['id'],
                'contractType': 'Supply', 'reason': 'Complete scope and confirmed delivery',
            }, accepted=(200, 409))[0]
        statuses = parallel([lambda p=p: award(p) for p in packages] + [lambda: award(packages[0])])
        assert sorted((statuses[0], statuses[-1])) == [200, 409], statuses
        assert statuses[1:-1] == [200] * (len(packages) - 1), statuses
        contract_ids = []
        for package in packages:
            _, final = call('procurement', package['path'])
            assert final['header']['status'] == 'Awarded'
            assert sum(e['action'] == 'awarded' for e in final['events']) == 1
            assert len(final['bids']) == 2
            snapshot = json.loads(final['awardSnapshotJson'])
            assert len(snapshot['Bids']) == 2
            assert len(snapshot['Events']) == 5  # create, issue, two revisions, evaluate
            contract_ids.append(final['contractId'])
        assert len(set(contract_ids)) == len(packages) and all(contract_ids)
        print(f'Round {round_number + 1}: {len(packages)} issued/revised/evaluated/awarded; '
              f'competing award 200/409; unique contracts {contract_ids}', flush=True)


if __name__ == '__main__':
    main()
