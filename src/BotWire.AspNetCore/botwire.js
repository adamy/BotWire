"use strict";(()=>{var b="botwire_session";function v(n,l){let t=l==="bottom-left"?"left":"right";return`
*,*::before,*::after{box-sizing:border-box;margin:0;padding:0}

#bubble{
  position:fixed;bottom:24px;${t}:24px;
  width:56px;height:56px;
  background:${n};border:none;border-radius:50%;cursor:pointer;
  display:flex;align-items:center;justify-content:center;
  box-shadow:0 4px 20px rgba(0,0,0,.28);
  transition:transform .2s ease,box-shadow .2s ease;
  z-index:2147483646;color:#fff;
}
#bubble:hover{transform:scale(1.08);box-shadow:0 6px 24px rgba(0,0,0,.32)}
#bubble svg{width:26px;height:26px;fill:#fff;pointer-events:none;flex-shrink:0}

#panel{
  position:fixed;bottom:96px;${t}:20px;
  width:360px;min-height:400px;max-height:560px;
  background:#fff;border-radius:16px;
  box-shadow:0 8px 48px rgba(0,0,0,.18);
  display:flex;flex-direction:column;overflow:hidden;
  z-index:2147483646;
  animation:bw-in .2s ease-out;
}
#panel[hidden]{display:none!important}
@keyframes bw-in{from{transform:scale(.92) translateY(8px);opacity:0}to{transform:none;opacity:1}}

#header{
  display:flex;align-items:center;justify-content:space-between;gap:8px;
  padding:14px 16px;background:${n};color:#fff;flex-shrink:0;
}
#header-title{font-weight:600;font-size:15px}
#close{
  background:none;border:none;color:#fff;cursor:pointer;
  font-size:20px;line-height:1;padding:2px 6px;border-radius:6px;opacity:.75;
}
#close:hover{opacity:1;background:rgba(255,255,255,.15)}

#messages{
  flex:1;overflow-y:auto;padding:12px 12px 4px;
  display:flex;flex-direction:column;gap:8px;
  scroll-behavior:smooth;
}
#messages:empty::before{
  content:'How can we help you today?';
  color:#94a3b8;font-size:13px;text-align:center;
  margin:auto;padding:32px 16px;
}

.msg{
  max-width:82%;padding:10px 14px;border-radius:14px;
  font-size:14px;line-height:1.55;word-break:break-word;
  animation:msg-in .15s ease-out;
}
@keyframes msg-in{from{transform:translateY(5px);opacity:0}to{transform:none;opacity:1}}
.msg-user{align-self:flex-end;background:${n};color:#fff;border-bottom-right-radius:3px}
.msg-bot {align-self:flex-start;background:#f1f5f9;color:#1e293b;border-bottom-left-radius:3px}
.msg-sys {
  align-self:center;background:#fef9c3;color:#854d0e;
  font-size:13px;border-radius:8px;text-align:center;max-width:90%;
}

#typing{padding:6px 12px 8px;display:flex;gap:4px;align-items:center;flex-shrink:0}
#typing[hidden]{display:none!important}
#typing span{
  width:7px;height:7px;background:#94a3b8;border-radius:50%;
  animation:bw-dot .9s infinite ease-in-out;
}
#typing span:nth-child(2){animation-delay:.15s}
#typing span:nth-child(3){animation-delay:.3s}
@keyframes bw-dot{0%,80%,100%{transform:scale(.6);opacity:.4}40%{transform:scale(1);opacity:1}}

#input-area{
  display:flex;gap:8px;align-items:flex-end;
  padding:10px 12px;border-top:1px solid #e2e8f0;flex-shrink:0;
}
#input-area[hidden]{display:none!important}
#input{
  flex:1;resize:none;border:1px solid #e2e8f0;border-radius:10px;
  padding:8px 12px;font:inherit;font-size:14px;outline:none;
  max-height:100px;overflow-y:auto;line-height:1.5;
  transition:border-color .15s;
}
#input:focus{border-color:${n};outline:none}
#send{
  background:${n};color:#fff;border:none;border-radius:10px;
  padding:9px 16px;cursor:pointer;font-size:14px;font-weight:500;
  white-space:nowrap;flex-shrink:0;transition:opacity .15s;
}
#send:disabled{opacity:.45;cursor:not-allowed}
#send:not(:disabled):hover{opacity:.88}

#contact-form{
  padding:14px 16px 16px;border-top:1px solid #e2e8f0;
  display:flex;flex-direction:column;gap:10px;flex-shrink:0;
}
#contact-form[hidden]{display:none!important}
#contact-form p{font-size:13px;color:#64748b;line-height:1.5}
#email-input{
  border:1px solid #e2e8f0;border-radius:10px;
  padding:9px 12px;font:inherit;font-size:14px;outline:none;
  transition:border-color .15s;
}
#email-input:focus{border-color:${n}}
#contact-submit{
  background:${n};color:#fff;border:none;border-radius:10px;
  padding:10px;cursor:pointer;font-size:14px;font-weight:500;
  transition:opacity .15s;
}
#contact-submit:hover{opacity:.88}

#ticket-card{
  margin:12px 12px 14px;padding:14px 16px;
  background:#f0fdf4;border:1px solid #bbf7d0;border-radius:12px;
  font-size:13px;color:#166534;text-align:center;line-height:1.55;
  flex-shrink:0;
}
#ticket-card[hidden]{display:none!important}

@media(max-width:480px){
  #panel{bottom:0;left:0;right:0;width:100%;min-height:unset;max-height:100%;
    border-radius:0;border-top-left-radius:16px;border-top-right-radius:16px}
  #bubble{bottom:16px;${t}:16px}
}
`}var f='<svg viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg"><path d="M20 2H4a2 2 0 00-2 2v18l4-4h14a2 2 0 002-2V4a2 2 0 00-2-2z"/></svg>',y='<svg viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg"><path d="M20 2H4a2 2 0 00-2 2v18l4-4h14a2 2 0 002-2V4a2 2 0 00-2-2zM6 10h12v2H6v-2zm0-4h12v2H6V6zm8 8H6v-2h8v2z"/></svg>',p=class extends HTMLElement{constructor(){super();this.sessionToken=null;this.streaming=!1;this.awaitingEmail=!1;this.shadow=this.attachShadow({mode:"open"})}get endpoint(){return this.dataset.endpoint??"/support"}get widgetTitle(){return this.dataset.title??"Support"}get primary(){return this.dataset.primaryColor??"#6366f1"}get position(){return this.dataset.position??"bottom-right"}get publicKey(){return this.dataset.publicKey}connectedCallback(){this.mount(),this.sessionToken=sessionStorage.getItem(b),this.sessionToken||this.initSession()}mount(){this.shadow.innerHTML=`
<style>${v(this.primary,this.position)}</style>
<button id="bubble" aria-label="Open support chat" aria-expanded="false">${f}</button>
<div id="panel" hidden role="dialog" aria-label="${this.esc(this.widgetTitle)} support chat">
  <div id="header">
    <span id="header-title">${this.esc(this.widgetTitle)}</span>
    <button id="close" aria-label="Close chat">\u2715</button>
  </div>
  <div id="messages" role="log" aria-live="polite" aria-relevant="additions"></div>
  <div id="typing" hidden aria-hidden="true"><span></span><span></span><span></span></div>
  <div id="input-area">
    <textarea id="input" placeholder="Type a message\u2026" rows="1" aria-label="Message input"></textarea>
    <button id="send" type="button">Send</button>
  </div>
  <form id="contact-form" hidden>
    <p>Please leave your email address so our team can follow up with you.</p>
    <input type="email" id="email-input" placeholder="your@email.com" required aria-label="Email address">
    <button id="contact-submit" type="submit">Submit</button>
  </form>
  <div id="ticket-card" hidden role="status"></div>
</div>`,this.panel=this.q("#panel"),this.bubble=this.q("#bubble"),this.messages=this.q("#messages"),this.typing=this.q("#typing"),this.inputArea=this.q("#input-area"),this.input=this.q("#input"),this.sendBtn=this.q("#send"),this.contact=this.q("#contact-form"),this.emailIn=this.q("#email-input"),this.ticket=this.q("#ticket-card"),this.bubble.addEventListener("click",()=>this.toggle()),this.q("#close").addEventListener("click",()=>this.close()),this.sendBtn.addEventListener("click",()=>this.handleSend()),this.input.addEventListener("keydown",e=>{e.key==="Enter"&&!e.shiftKey&&(e.preventDefault(),this.handleSend())}),this.input.addEventListener("input",()=>this.autoResize()),this.contact.addEventListener("submit",e=>{e.preventDefault(),this.handleContactSubmit()})}async initSession(){try{let e=await this.post(`${this.endpoint}/session`,{});if(e.ok){let t=await e.json();this.sessionToken=t.sessionToken,sessionStorage.setItem(b,t.sessionToken)}}catch{}}toggle(){this.panel.hidden?this.open():this.close()}open(){this.panel.hidden=!1,this.bubble.innerHTML=y,this.bubble.setAttribute("aria-expanded","true"),this.awaitingEmail?this.emailIn.focus():this.input.focus()}close(){this.panel.hidden=!0,this.bubble.innerHTML=f,this.bubble.setAttribute("aria-expanded","false")}handleSend(){if(this.streaming||this.awaitingEmail)return;let e=this.input.value.trim();e&&(this.input.value="",this.autoResize(),this.appendMessage("user",e),this.stream(e))}handleContactSubmit(){let e=this.emailIn.value.trim();e&&(this.contact.hidden=!0,this.awaitingEmail=!1,this.stream("",e))}async stream(e,t){if(this.streaming)return;this.streaming=!0,this.sendBtn.disabled=!0,this.typing.hidden=!1,this.sessionToken||await this.initSession();let i={message:e,sessionToken:this.sessionToken};t&&(i.contactEmail=t);let r=null;try{let s=await this.post(`${this.endpoint}/chat/stream`,i);if(!s.ok||!s.body){this.typing.hidden=!0,this.appendMessage("sys","Something went wrong. Please try again.");return}let m=s.body.getReader(),x=new TextDecoder,a="",h=!1;for(;!h;){let c=await m.read();if(c.done)break;a+=x.decode(c.value,{stream:!0});let d;for(;(d=a.indexOf(`
`))!==-1;){let u=a.slice(0,d);if(a=a.slice(d+1),!u.startsWith("data: "))continue;let g=u.slice(6);if(g==="[DONE]"){h=!0;break}let o;try{o=JSON.parse(g)}catch{continue}switch(this.typing.hidden=!0,o.type){case"token":r||(r=this.appendMessage("bot","")),r.textContent+=o.value,this.scrollBottom();break;case"collect_contact":this.inputArea.hidden=!0,this.contact.hidden=!1,this.awaitingEmail=!0,this.emailIn.focus();break;case"escalated":this.ticket.hidden=!1,this.ticket.textContent=`\u2713 Support ticket ${o.ticketId} created \u2014 we'll be in touch soon.`;break;case"blocked":this.appendMessage("sys",o.reason);break}}}}catch(s){this.typing.hidden=!0,s instanceof DOMException&&s.name==="AbortError"||this.appendMessage("sys","Connection error. Please try again.")}finally{this.typing.hidden=!0,this.streaming=!1,this.awaitingEmail||(this.sendBtn.disabled=!1,this.panel.hidden||this.input.focus())}}appendMessage(e,t){let i=document.createElement("div");return i.className=`msg msg-${e}`,i.textContent=t,this.messages.appendChild(i),this.scrollBottom(),i}scrollBottom(){this.messages.scrollTop=this.messages.scrollHeight}autoResize(){this.input.style.height="auto",this.input.style.height=`${Math.min(this.input.scrollHeight,100)}px`}post(e,t){let i={"Content-Type":"application/json"};return this.publicKey&&(i["X-BotWire-Key"]=this.publicKey),fetch(e,{method:"POST",headers:i,body:JSON.stringify(t)})}esc(e){return e.replace(/&/g,"&amp;").replace(/</g,"&lt;").replace(/>/g,"&gt;").replace(/"/g,"&quot;")}q(e){return this.shadow.querySelector(e)}};customElements.define("botwire-widget",p);})();
