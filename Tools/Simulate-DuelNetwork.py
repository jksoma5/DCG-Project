"""Local UDP impairment proxy for NGO integration checks (no third-party libraries).

Run a smoke host on 17777, then connect the smoke guest with -dcgDuelPort 17778.
The proxy adds 35 +/- 20ms one-way delay and drops 5% of datagrams by default.
"""
import argparse
import heapq
import random
import select
import socket
import time

p=argparse.ArgumentParser()
p.add_argument('--seconds',type=float,default=120)
p.add_argument('--delay-ms',type=float,default=35)
p.add_argument('--jitter-ms',type=float,default=20)
p.add_argument('--loss',type=float,default=.05)
p.add_argument('--listen',type=int,default=17778)
p.add_argument('--host',type=int,default=17777)
args=p.parse_args()
rng=random.Random(4921)
host=('127.0.0.1',args.host)
guest=None
pending=[]
sent=dropped=sequence=0
with socket.socket(socket.AF_INET,socket.SOCK_DGRAM) as sock:
    sock.bind(('127.0.0.1',args.listen)); sock.setblocking(False)
    deadline=time.monotonic()+args.seconds
    next_report=time.monotonic()+5
    print('UDP impairment proxy ready',flush=True)
    while time.monotonic()<deadline:
        now=time.monotonic()
        if now>=next_report:
            print(f"forwarded={sent} dropped={dropped}",flush=True);next_report=now+5
        while pending and pending[0][0]<=now:
            _,_,data,target=heapq.heappop(pending)
            sock.sendto(data,target); sent+=1
        ready,_,_=select.select([sock],[],[],.002)
        if not ready: continue
        data,source=sock.recvfrom(65535)
        if source==host:
            target=guest
        else:
            guest=source; target=host
        if target is None: continue
        if rng.random()<args.loss: dropped+=1; continue
        delay=max(0,args.delay_ms+rng.uniform(-args.jitter_ms,args.jitter_ms))/1000
        sequence+=1; heapq.heappush(pending,(now+delay,sequence,data,target))
print(f'forwarded={sent} dropped={dropped}',flush=True)
